using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;

namespace PreferencesPlugin
{
    public class PreferencesPlugin : IConfigurablePlugin, IPromptAdderPlugin, IExecutorPlugin
    {
        private PreferencesConfiguration _config = new();

        public string Name => "PreferencesPlugin";

        public string Description => "Stores and adds Preferences as Key-Value-Pair";

        public string ParameterFormat => @"Action:'{setPreference/removePreference}', Key:'{preference Name}', Value:'{preference content}'";

        public string PromptAddOn
        {
            get
            {
                StringBuilder promptAddon = new StringBuilder("The preferences of the user are:\n");
                foreach (var (key, value) in _config.Preferences)
                {
                    promptAddon.AppendLine($"\tThe '{key}' is '{value}'.");
                }
                promptAddon.AppendLine();
                return promptAddon.ToString();
            }
        }

        public event ConfigurationChangeEventHandler OnConfigurationChange;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<string> ExecuteAsync(string parameter)
        {
            Log.Debug("Start execution of the PreferencesPlugin");

            parameter = parameter.ToLower();
            Log.Debug("[PreferencesPlugin]: Recieved Parameter {parameter}", parameter);
            var actionRegex = new Regex(@"((?:setpreference)|(?:removepreference))");
            var keyRegex = new Regex(@".*key:([^,]*),?");
            var valueRegex = new Regex(@".*value:([^,]*),?");

            var action = actionRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);
            var key = keyRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);

            StringBuilder messages = new StringBuilder();

            try
            {
                switch (action.Trim())
                {
                    case "setpreference":
                        var value = valueRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);
                        if (key == string.Empty || value == string.Empty)
                            break;
                        _config.Preferences[key] = value;
                        OnConfigurationChange(this);
                        string message = $"Added '{key}' with '{value}' as preference.";
                        Log.Information("[Preferences Plugin]: {message}", message);
                        messages.AppendLine(message);
                        break;
                    case "removepreference":
                        if (_config.Preferences.Remove(key))
                        {
                            message = $"Removed '{key}' from the preferences list";
                            messages.Append(message);
                            OnConfigurationChange(this);
                            Log.Information("[Preferences Pluign]: {message}", message);
                        }
                        else
                        {
                            message = $"Tried to remove nonexisting Key '{key}.";
                            messages.Append(message);
                            Log.Information("[Preferences Pluign]: {message}", message);
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                messages.AppendLine($"An error occoured trying to perfom '{parameter}'.");
                Log.Error(ex, messages.ToString());
            }

            return Task.FromResult<string>(messages.ToString());
        }

        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(_config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public void LoadConfiguration(JsonNode configuration)
        {
            _config = configuration.Deserialize<PreferencesConfiguration>() ?? throw new InvalidDataException("The config could not be loaded.");
        }

        private static readonly char[] TRIM_CHARS = { '\'', '\"', '{', '}', ',', '.', ' ' };
    }

    [Serializable]
    public class PreferencesConfiguration
    {
        public Dictionary<string, string> Preferences { get; set; } = [];
    }
}
