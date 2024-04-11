using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;

namespace PreferencesPlugin;

public class PreferencesPlugin : IConfigurablePlugin, IPromptAdderPlugin, IExecutorPlugin
{
    #region Public

    public string Name => "PreferencesPlugin";
    public string Description => "This plugin returns the preferences of the user.";
    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<PreferencesPluginConfig>()
                  ?? throw new InvalidDataException("The config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public string PromptAddOn
    {
        get
        {
            // Convert both default and customized preferences to dictionaries.
            var defaultPrefsDict = ConvertPreferencesToDictionary(_config.DefaultPreferences);
            var customPrefsDict = ConvertPreferencesToDictionary(_config.CustomizedPreferences);

            // Merge the dictionaries, prioritizing customized preferences.
            var mergedPrefsDict = new Dictionary<string, string>(defaultPrefsDict, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in customPrefsDict)
            {
                // This will add the key-value pair if the key does not exist, or update the value if it does.
                mergedPrefsDict[kvp.Key] = kvp.Value;
            }

            // Convert the merged dictionary back to a string.
            return ConvertDictionaryToPreferences(mergedPrefsDict);
        }
    }

    public string ParameterFormat => "Add:key_value;Add:key_value;...;Remove:key;Remove:key;...;";

    public Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Starting execution of PreferencesPlugin.");

        try
        {
            var preferencesDict = ConvertPreferencesToDictionary(_config.CustomizedPreferences);

            foreach (var command in ParseCommands(parameter))
            {
                try
                {
                    ProcessCommand(command, preferencesDict);
                }
                catch (CommandParsingException e)
                {
                    Log.Warning(e.Message);
                }
            }

            _config.CustomizedPreferences = ConvertDictionaryToPreferences(preferencesDict);
            OnConfigurationChange?.Invoke(this);

            Log.Debug("[{PluginName}]: Customized preferences: {Preferences}",
                nameof(PreferencesPlugin), _config.CustomizedPreferences);
            return Task.FromResult(_config.CustomizedPreferences);
        }
        catch (Exception e)
        {
            Log.Error($"An error has occurred while updating the user preferences:\n" +
                      $" {e.Source}: {e.Message}");
            return Task.FromResult("An error has occurred while updating the user preferences.");
        }
    }

    #endregion

    #region Private

    private PreferencesPluginConfig _config = new();

    private IEnumerable<CommandModel> ParseCommands(string parameter)
    {
        char[] charsToTrim = [ '-', ':', '{', '}', '*', ',', '.', ' ', '\'', '\"' ];
        var commands = parameter.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        IList<CommandModel> preferenceOperations = new List<CommandModel>();
        foreach (var cmd in commands)
        {
            try
            {
                var parts = cmd.Split([':']);
                if (parts.Length != 2)
                {
                    throw new CommandParsingException(cmd);
                }

                var action = parts[0].Trim(charsToTrim).ToLower();
                var rest = parts[1].Split(['_']);
                if (rest.Length != 1 && rest.Length != 2)
                {
                    throw new CommandParsingException(cmd);
                }

                var key = rest[0].Trim(charsToTrim).ToLower();
                var value = rest.Length > 1 ? rest[1].Trim(charsToTrim) : null; // Value might be null for "Remove" actions

                preferenceOperations.Add(new CommandModel { Action = action, Key = key, Value = value });
            }
            catch (CommandParsingException e)
            {
                Log.Warning(e.Message);
            }
        }
        return preferenceOperations;
    }

    private void ProcessCommand(CommandModel command, IDictionary<string, string> preferencesDict)
    {
        switch (command.Action)
        {
            case "add":
                preferencesDict[command.Key] = command.Value ?? throw new CommandParsingException(command.ToString());
                break;
            case "remove":
                preferencesDict.Remove(command.Key);
                break;
            default:
                throw new CommandParsingException(command.ToString());
        }
    }

    private Dictionary<string, string> ConvertPreferencesToDictionary(string preferences)
    {
        return preferences.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
    }

    private string ConvertDictionaryToPreferences(Dictionary<string, string> dict)
    {
        return string.Join("|", dict.Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    #endregion

}

[Serializable]
internal class PreferencesPluginConfig
{
    public string DefaultPreferences { get; set; } = "language:english|residence:Konrad-Adenauer-Straße 3, 70173 Stuttgart";
    public string CustomizedPreferences { get; set; } = "";
}
