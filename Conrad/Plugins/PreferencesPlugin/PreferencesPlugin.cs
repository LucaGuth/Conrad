using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;

namespace PreferencesPlugin;

public class PreferencesPlugin : IConfigurablePlugin, IPromptAdderPlugin, IExecutorPlugin
{
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
        _config = configuration.Deserialize<PreferencesPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    private PreferencesPluginConfig _config = new();

    public string PromptAddOn => _config.Preferences;

    public string ParameterFormat => "Add:key_value;Add:key_value;Add:key_value;Remove:key_value;Remove:key_value;";

    public Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the PreferencesPlugin");
        // Split the parameter string on ";" to separate Add and Remove commands.
        var commands = parameter.Split(';', StringSplitOptions.RemoveEmptyEntries);

        // Assuming _config.Preferences is a string representing key-value pairs,
        // e.g., "key1:value1,key2:value2", first convert it to a dictionary for easier manipulation.
        var preferencesDict = _config.Preferences.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => p.Split(':'))
                                    .Where(parts => parts.Length == 2)
                                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var command in commands)
        {
            char[] charsToTrim = [ '-', ':', '{', '}', '*', ',', '.', ' ', '\'', '\"' ];
            var parts = command.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                continue;

            var action = parts[0].Trim(charsToTrim);
            var keyValue = parts[1];

            var items = keyValue.Split('_', StringSplitOptions.RemoveEmptyEntries);

            if (items.Length != 2)
                continue;
            var key = items[0].Trim(charsToTrim);
            var value = items[1].Trim(charsToTrim);

            switch (action.ToLower())
            {
                case "add":
                    // Add or update the key-value pair in the dictionary.
                    preferencesDict[key] = value;
                    break;

                case "remove":
                    // Remove the key from the dictionary if it exists.
                    preferencesDict.Remove(key, out _);
                    break;
            }
        }

        // Convert the dictionary back to the string format and store it in _config.Preferences.
        _config.Preferences = string.Join(",", preferencesDict.Select(kv => $"{kv.Key}:{kv.Value}"));

        OnConfigurationChange?.Invoke(this);

        Log.Debug("[{PluginName}]: Updated preferences: {Preferences}", nameof(PreferencesPlugin), _config.Preferences);

        // Since this method doesn't need to be asynchronous, return a completed task.
        // If your real scenario involves asynchronous operations, you can await them as needed.
        return Task.FromResult(string.Empty);
    }
}

[Serializable]
internal class PreferencesPluginConfig
{
    public string Preferences { get; set; } = "";
}
