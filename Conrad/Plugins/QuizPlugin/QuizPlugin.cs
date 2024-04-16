using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;

namespace QuizPlugin;

public class QuizPlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "QuizProvider";

    public string Description => "This plugin provides three random quiz questions with their answers for a random .";

    public string ParameterFormat => "RandomParameter:'{parameter}'\n" +
                                     "\tThe parameter must be any string.";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the QuizPlugin");
        try
        {
            var responseJson = await GetQuizAsync();

            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            var results = root.GetProperty("results");
            var quiz = GetQuestions(results);

            return $"Did you know the following about {WebUtility.HtmlDecode(results[0].GetProperty("category").GetString())}" +
                   $"?\n\t{string.Join("\n\t", quiz)}";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException:
                    return e.Message;
                default:
                    Log.Error($"An error has occurred while retrieving the quiz information:\n" +
                              $" {e.Source}: {e.Message}");
                    return "An error has occurred while retrieving the quiz information.";
            }
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
        _config = configuration.Deserialize<QuizPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    #endregion

    #region Private

    private readonly HttpClient _httpClient = new();

    private QuizPluginConfig _config = new();

    private IEnumerable<string> GetQuestions(JsonElement results)
    {
        // Iterate through each quiz item in the results array
        return from quizItem in results.EnumerateArray()
            let question = quizItem.GetProperty("question").GetString()
            let correctAnswer = quizItem.GetProperty("correct_answer").GetString()
            select $"Question: {WebUtility.HtmlDecode(question)} Answer: {correctAnswer}";
    }

    private async Task<string> GetQuizAsync()
    {
        var category = GetRandomCategory();
        var url = $"{_config.BaseUrl}?amount=3&type=boolean&category={category}";
        try {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Log.Error($"{e.StatusCode}: {e.Message}");
            if (e.StatusCode == HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("This plugin can only be used once every 5 seconds. Please try again later.");
            throw new HttpRequestException("The quiz information could not be retrieved.");
        }
    }

    private int GetRandomCategory()
    {
        int[] numbers = [9, 16, 17, 18, 19, 21, 22, 23, 24, 27, 28, 30];

        var random = new Random();

        // Generating a random index between 0 and the length of the array - 1
        var randomIndex = random.Next(numbers.Length);

        return numbers[randomIndex];
    }

    #endregion
}

[Serializable]
internal class QuizPluginConfig
{
    public string BaseUrl { get; set; } = "https://opentdb.com/api.php";
}
