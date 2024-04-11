namespace PreferencesPlugin;

internal readonly struct CommandModel
{
    public string Action { get; init; }
    public string Key { get; init; }
    public string? Value { get; init; }

    public override string ToString() => $"{Action}:{Key}_{Value}";
}
