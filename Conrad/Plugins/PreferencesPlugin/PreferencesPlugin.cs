using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;

namespace PreferencesPlugin;

public class PreferencesPlugin : IConfigurablePlugin, IPromptAdderPlugin, IExecutorPlugin
{
    #region Public

    public string Name => "UserPreferencesPlugin";
    public string Description => "This plugin returns the preferences of the user.";

    public string ParameterFormat => @"Action:'{Add/Remove}', Key:'{key}', Value:'{value}'";
        //"Action:'{Add/Remove}', Key:'{key}', Value:'{value}'\n" +
        //                             "\tThe 'Action' parameter is required and can be either 'Add' or 'Remove'.\n" +
        //                             "\tThe 'Key' parameter is required and must be a string.\n" +
        //                             "\tThe 'Value' parameter is required for 'Add' actions and should specify the " +
        //                             "value associated with the key.\n" +
        //                             "\tFor an 'Add' action with an existing key, you can modify the current key-value settings.\n" +
        //                             "\tA valid parameter format to add or modify a preference:\n" +
        //                             "\t\tAction:'Add', Key:'language', Value:'english'\n" +
        //                             "\tA valid parameter format to remove a preference:\n" +
        //                             "\t\tAction:'Remove', Key:'language'\n" +
        //                             "\tIMPORTANT: Do NOT customize the preferences unless you are absolutely sure it " +
        //                             "is necessary. Avoid modifying preferences if a similar key already exists and " +
        //                             "can be adjusted instead.";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

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

    public Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Starting execution of PreferencesPlugin.");

        try
        {
            var preferencesDict = ConvertPreferencesToDictionary(_config.CustomizedPreferences);
            ProcessCommand(ParseCommands(parameter), preferencesDict);

            _config.CustomizedPreferences = ConvertDictionaryToPreferences(preferencesDict);
            OnConfigurationChange?.Invoke(this);

            Log.Debug("[{PluginName}]: Customized preferences: {Preferences}",
                nameof(PreferencesPlugin), _config.CustomizedPreferences);
            return Task.FromResult(_config.CustomizedPreferences);
        }
        catch (Exception e)
        {
            if (e is CommandParsingException)
            {
                Log.Warning($"The preferences could not be updated due to an invalid command: {parameter}");
                return Task.FromResult("The preferences could not be updated due to an invalid command.");
            }

            Log.Error($"An error has occurred while updating the user preferences:\n" +
                      $" {e.Source}: {e.Message}");
            return Task.FromResult("An error has occurred while updating the user preferences.");
        }
    }

    #endregion

    #region Private

    private PreferencesPluginConfig _config = new();

    private CommandModel ParseCommands(string parameter)
    {
        char[] charsToTrim = [ '-', ':', '{', '}', '*', ',', '.', ' ', '\'', '\"' ];
        // Regex pattern to match Action, Key, and optional Value
        // Supports different types of brackets or quotes
        const string pattern = """((?:ac).*?(('(?<action>[^']+))|({(?<action>[^}]+))|(\*(?<action>[^\*]+)\*))).*?|((?:key).*?(('(?<key>[^']+))|({(?<key>[^}]+))|(\*(?<key>[^\*]+)\*))).*?|((?:val).*?(('(?<val>[^']+))|({(?<value>[^}]+))|(\*(?<value>[^\*]+)\*))).*?""";

        var matches = Regex.Matches(parameter, pattern, RegexOptions.IgnoreCase);

        string? action = null, key = null, value = null;

        foreach (Match match in matches)
        {
            if (match.Groups["action"].Success)
            {
                if (action != null)
                    throw new CommandParsingException(parameter);
                action = match.Groups["action"].Value.Trim(charsToTrim).ToLower();
            }

            if (match.Groups["key"].Success)
            {
                if (key != null)
                    throw new CommandParsingException(parameter);
                key = match.Groups["key"].Value.Trim(charsToTrim).ToLower();
            }

            if (match.Groups["value"].Success)
            {
                if (value != null)
                    throw new CommandParsingException(parameter);
                value = match.Groups["value"].Value.Trim(charsToTrim).ToLower();
            }
        }

        if (action == null || key == null)
            throw new CommandParsingException(parameter);
        if (action == "add" && value == null)
            throw new CommandParsingException(parameter);

        var commandModel = new CommandModel
        {
            Action = action,
            Key = key,
            Value = value
        };

        Log.Debug("Parsed command: {Command}", commandModel);

        return commandModel;
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
