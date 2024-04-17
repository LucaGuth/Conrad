using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;
using Exception = System.Exception;

namespace UseCaseManagerPlugin;

public class UseCaseManagerPlugin : INotifierPlugin, IExecutorPlugin, IConfigurablePlugin
{
    public string Name => "UseCaseManager";

    public string Description => "This plugin manages the use cases which combines information.";

    public event NotifyEventHandler? OnNotify;

    public void Run()
    {
        while (true)
        {
            var useCase = CheckForNotification();

            var temp = "null";
            if (useCase != null)
                temp = useCase.Name;

            if (useCase is not null)
            {

                _config.UseCases.First(c => c.Name == useCase.Name).InvocedLastTime = DateTime.Now;
                OnNotify?.Invoke(this, $"Please greet the user by name. {useCase.Description}");
                OnConfigurationChange?.Invoke(this);
            }

            Task.Delay(1000).Wait();
        }
    }

    private UseCaseModel? CheckForNotification()
    {
        var nowDateTimeOffset = DateTime.Now.AddSeconds(_config.InvocationOffsetInSeconds).TimeOfDay;
        return _config.UseCases.FirstOrDefault(c =>
            c.InvocedLastTime.Date != DateTime.Now.Date && c.InvocationTime.TimeOfDay <= nowDateTimeOffset);
    }

    public string ParameterFormat => "Action:'set/remove', UseCaseName:'{name}', UseCaseAt:'{HH:mm:ss}', Description:'{descriptiveText}'";

    public Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the UseCaseManagerPlugin");
        try
        {
            var (action, useCase) = ExtractParameters(parameter);
            var result = new StringBuilder();
            switch (action)
            {
                case "set":
                    var existingUseCase = _config.UseCases.FirstOrDefault(c => c.Name == useCase.Name);
                    if (existingUseCase is not null)
                    {
                        existingUseCase.InvocationTime = useCase.InvocationTime;
                        existingUseCase.Description = useCase.Description;
                        result.AppendLine($"The UseCase '{useCase.Name}' was updated with the new time " +
                                          $"'{useCase.InvocationTime}' and description '{useCase.Description}'.");
                    }
                    else
                    {
                        _config.UseCases.Add(useCase);
                        result.AppendLine($"The UseCase '{useCase.Name}' was added with the time " +
                                          $"'{useCase.InvocationTime}' and description '{useCase.Description}'.");
                    }
                    break;
                case "remove":
                    _config.UseCases.RemoveAll(c => c.Name == useCase.Name);
                    result.AppendLine($"The UseCase '{useCase.Name}' was removed.");
                    break;
            }
            OnConfigurationChange?.Invoke(this);
            return Task.FromResult(result.ToString());
        }
        catch (Exception e)
        {
            if (e is ArgumentException)
                return Task.FromResult(e.Message);

            Log.Error("An error occurred while processing the information data:\n{Source}: " +
                      "{Message}\n{StackTrace}", e.Source, e.Message, e.StackTrace);
            return Task.FromResult("An error occurred while processing the input data for the UseCaseManager.");
        }
    }

    private static readonly char[] TrimChars = ['\'', '\"', '{', '}', ',', '.', ' '];

    private (string, UseCaseModel) ExtractParameters(string parameter)
    {
        parameter = parameter.ToLower();
        var actionRegex = new Regex(@"((?:set)|(?:remove))");
        var nameRegex = new Regex(@".*name:([^,]*),?");
        var timeRegex = new Regex(@"\d{2}:\d{2}:\d{2}");
        var descriptionRegex = new Regex(".*description:([^,]*),?");
        try
        {
            var action = actionRegex.Match(parameter).Groups[1].Value.Trim(TrimChars);
            var name = nameRegex.Match(parameter).Groups[1].Value.Trim(TrimChars);
            var useCase = new UseCaseModel(name);

            switch (action)
            {
                case "set":
                    var time = DateTime.Parse(timeRegex.Match(parameter).Groups[1].Value.Trim(TrimChars));
                    var description = descriptionRegex.Match(parameter).Groups[1].Value.Trim(TrimChars);
                    useCase.InvocationTime = time;
                    useCase.Description = description;
                    Log.Information("The parameters are extracted: '{action}' '{name}' at '{time}': '{description}'.",
                        action, useCase.Name, useCase.InvocationTime, useCase.Description);
                    break;
                case "remove":
                    Log.Information("The parameters are extracted: '{action}' '{name}'.",
                        action, useCase.Name);
                    break;
                default:
                    Log.Error("The action could not be extracted from the parameter: {parameter}", parameter);
                    throw new ArgumentException("The action could not be extracted from the input.");
            }

            Log.Information("The parameters are extracted: '{action}' '{name}' at '{time}': '{description}'.",
                action, useCase.Name, useCase.InvocationTime, useCase.Description);
            return (action, useCase);
        }
        catch (Exception e)
        {
            if (e is ArgumentException)
                throw;

            Log.Error("The time could not be extracted from the parameter: {parameter}", parameter);
            throw new ArgumentException("The time could not be extracted from the input.");
        }

    }

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<InformationNotifierPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    private InformationNotifierPluginConfig _config = new();
}

[Serializable]
public class InformationNotifierPluginConfig
{
    public List<UseCaseModel> UseCases { get; set; } =
    [
        new UseCaseModel("InformationUseCase")
        {
            Description = "What is the latest news? What will the weather be like where I live tomorrow and what do " +
                          "you recommend I wear? Please give me stock information about my favourite stock company.",
            InvocationTime = DateTime.Parse("20:00:00")
        },
        new UseCaseModel("EntertainmentUseCase")
        {
            Description = "Please use the WikipediaPlugin to tell me something about an interesting topic in the IT " +
                          "sector and the TechnologyTrendsInformer to tell me about the latest technology trends. " +
                          "Please use the QuizPlugin to provide three questions and their answers.",
            InvocationTime = DateTime.Parse("16:00:00")
        },
        new UseCaseModel("CulinaryDelightsUseCase")
        {
            Description = "Please use the DishPlugin to tell me a recipe for a delicious dish in my favourite " +
                          "cuisine and the CoffeeBeverageProvider to tell me about a coffee beverage I can try." +
                          "Do you know any restaurants when I don't want to cook?",
            InvocationTime = DateTime.Parse("12:00:00")
        },
        new UseCaseModel("MorningUseCase")
        {
            Description = "Please use the WeatherPlugin to tell me about the weather today and the CalendarProvider " +
                          "to tell me about my appointments. Please use the DB Train Information to tell me how I get " +
                          "to work when I am at the railway station in one hour.",
            InvocationTime = DateTime.Parse("07:00:00")
        },
        new UseCaseModel("TestUseCase")
        {
            Description = "Please list all your functions.",
            InvocationTime = DateTime.Parse("12:10:00")
        }
    ];

    public int InvocationOffsetInSeconds { get; set; } = 30;

}
