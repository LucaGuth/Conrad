namespace InformationNotifierPlugin;

public class UseCaseModel(string name)
{
    public string Name { get; set; } = name;
    public string Description { get; set; } = string.Empty;
    public DateTime InvocationTime { get; set; } = DateTime.MinValue;
    public DateTime InvocedLastTime { get; set; } = DateTime.MaxValue;
}
