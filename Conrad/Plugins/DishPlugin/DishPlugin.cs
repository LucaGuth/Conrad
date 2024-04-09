using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;
using static System.String;

namespace FoodPlugin;

public class DishPlugin : IExecutorPlugin, IConfigurablePlugin
{
    private readonly HttpClient _client = new();

    public string Name => "Dish Suggestion Provider";
    public string Description => "Plugin to suggest a dish recipe based on the given dish and cuisine.";
    public string ParameterFormat => "dish:'{dish}', cuisine:'{cuisine}'\n\t" +
                                     "The two parameters dish and cuisine are required and must be strings. If the " +
                                     "two parameters are not provided or invalid, the plugin will use the default " +
                                     "parameters.\n\tA valid parameter format would be:\n\t" +
                                     "dish:'pasta', cuisine:'italian'";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the DishPlugin");
        try
        {
            var (dish, cuisine) = ParseInput(parameter);
            var dishIdsResponseString = await GetDishIdsAsync(dish, cuisine);
            var dishRaw = ParseAndFormatDishIdResponse(dishIdsResponseString);
            var recipeResponseString = await GetRecipeAsync(dishRaw.Id);
            var ingredients = ParseAndFormatDishRecipeResponse(recipeResponseString);
            return $"Dish title: {dishRaw.Title}\nDish Ingredients: {ingredients}";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or ArgumentException:
                    return e.Message;
                default:
                    Log.Error($"An error has occurred while retrieving the dish information:\n" +
                              $" {e.Source}: {e.Message}");
                    return "An error has occurred while retrieving the dish information.";
            }
        }
    }

    private static string ParseAndFormatDishRecipeResponse(string recipeResponseString)
    {
        using var document = JsonDocument.Parse(recipeResponseString);
        var root = document.RootElement;
        var ingredients = root.GetProperty("ingredients");

        // Using a HashSet to store ingredient names ensures uniqueness
        var ingredientNames = new HashSet<string>();
        try
        {
            foreach (var name in ingredients.EnumerateArray().Select(ingredient =>
                         ingredient.GetProperty("name").GetString()).OfType<string>())
            {
                ingredientNames.Add(name);
            }

            // Joining the unique ingredient names with ", " and returning the result
            return ingredientNames.Count > 0 ? Join(", ", ingredientNames) :
                throw new ArgumentException("The dish information could not be processed.");
        }
        catch (ArgumentException e)
        {
            Log.Error("Error while parsing the dish recipe response: {Message}", e.Message);
            throw new ArgumentException("The dish information could not be processed.");
        }

    }

    private static (string Title, string Id) ParseAndFormatDishIdResponse(string dishIdsResponseString)
    {
        using var doc = JsonDocument.Parse(dishIdsResponseString);
        var root = doc.RootElement;

        if (root.GetProperty("totalResults").GetInt32() == 0)
            throw new HttpRequestException("The dish could not be found.");

        var results = root.GetProperty("results");
        var firstResult = results[0];
        var title = firstResult.GetProperty("title").GetString();
        var id = firstResult.GetProperty("id").GetInt32().ToString();

        if (!IsNullOrEmpty(title) && !IsNullOrEmpty(id)) return (title, id);

        Log.Error("The response of the dish id request could not be parsed.\n{Response}",
            dishIdsResponseString);
        throw new ArgumentException("The dish information could not be processed.");
    }

    private (string dish, string cuisine) ParseInput(string parameter)
    {
        // Regular expression pattern to extract dish and cuisine
        const string pattern = @"dish:\s*'([^']*)',\s*cuisine:\s*'([^']*)'";

        // Try to match the pattern to the input string
        var match = Regex.Match(parameter, pattern);

        if (match.Success)
        {
            // If the pattern is matched, extract the groups corresponding to dish and cuisine
            char[] charsToTrim = ['{', '}', '(', ')', '*', ',', '.', ' '];
            var dish = match.Groups[1].Value.Trim(charsToTrim);
            var cuisine = match.Groups[2].Value.Trim(charsToTrim);

            return (dish, cuisine);
        }

        // If the pattern is not matched, log the warning and return the default parameters
        Log.Warning("The input parameter: {Parameter} does not match the required format for the" +
                  " dish and cuisine. The default parameters of the plugin config will be used", parameter);

        return (_config.Dish, _config.Cuisine);
    }

    private async Task<string> GetDishIdsAsync(string dish, string cuisine)
    {
        var url = $"{_config.BaseUrl}/complexSearch?apiKey={_config.ApiKey}&query={dish}&cuisine={cuisine}";
        return await PerformRequest(url);
    }

    private async Task<string> GetRecipeAsync(string id)
    {
        var url = $"{_config.BaseUrl}/{id}/ingredientWidget.json?apiKey={_config.ApiKey}";
        return await PerformRequest(url);
    }

    private async Task<string> PerformRequest(string url)
    {
        try
        {
            var response = await _client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var responseString = await response.Content.ReadAsStringAsync();
                    return responseString;
        }
        catch (HttpRequestException e)
        {
            Log.Error($"{e.StatusCode}: {e.Message}");
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new HttpRequestException("The dish could not be found.");
            }
            throw new HttpRequestException("The dish information could not be retrieved.");
        }
    }

    private FoodPluginConfig _config = new();

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<FoodPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;
}

[Serializable]
internal class FoodPluginConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.spoonacular.com/recipes/";
    public string Cuisine { get; set; } = "italian";
    public string Dish { get; set; } = "pasta";
}
