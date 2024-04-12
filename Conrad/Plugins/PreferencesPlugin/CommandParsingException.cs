namespace PreferencesPlugin;

internal class CommandParsingException(string invalidCommand)
    : Exception($"Rejected invalid preference command: {invalidCommand}");
