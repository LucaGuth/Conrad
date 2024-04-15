using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;

namespace CoffeePlugin;

public class CoffeePlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "CoffeeBeverageProvider";
    
    public string Description => "This plugin returns a random coffee beverage taking filter parameters into account.";
    
    public string ParameterFormat => "ListOfFilters:'{filter1, filter2, ...}'\n" +
                                     "\tThe parameter must be a list of strings. The existing filters are:\n" +
                                     "\tclassic, with milk, warm, without milk, with cream, cold, milk frother, cold brew, fruity, sweet foam\n" +
                                     "\tA valid parameter format would be:\n" +
                                     "\tListOfFilters:'with milk, cold'\n" +
                                     "\tIf the ListOfFilters is empty, no filters will be applied.";
    
    public event ConfigurationChangeEventHandler? OnConfigurationChange;
    
    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the CoffeePlugin");
        try
        {
            var filterTexts = ExtractFilters(parameter);
            Log.Debug("[{PluginName}] Parsed parameter: ListOfFilters:'{Filters}'",
                nameof(CoffeePlugin), string.Join(", ", filterTexts));
            var filterIds = MapToIds(filterTexts).ToList();
            var(title, description, preparation) = await GetRandomCoffeeBeverage(filterIds);
            return $"You can prepare the delicious coffee beverage '{title}', which has the description '{description}'," +
                   $" as follows: {preparation} For more information, please visit the Jura website.";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException:
                    return e.Message;
                default:
                    Log.Error($"An error has occurred while retrieving the wikipedia information:\n" +
                              $" {e.Source}: {e.Message}");
                    return "An error has occurred while retrieving the wikipedia information.";
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
        _config = configuration.Deserialize<CoffeePluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    #endregion

    #region Private

    private readonly HttpClient _httpClient = new();
    private CoffeePluginConfig _config = new();

    private readonly Dictionary<string, string> _filters = new()
    {
        {"classic", "AA852CD1A655406A8E63DCF7707DC113"},
        {"with milk", "5E3E8CF97CA04B95A857C8A20D27EB5D"},
        {"warm", "70119DBFED7847C8A5819ED8FE23DE32"},
        {"without milk", "CA88EA81B5814E88BA8848F68458FE83"},
        {"with cream", "50E460CDA0DB41E8AAB239D2F313B1BF"},
        {"cold", "2FA778752B5241BFA34F20A384877ADF"},
        {"milk frother", "D0931460F83049F197ECB9A9CDEF5B33"},
        {"cold brew", "FF331A61ADDC4F25B6FC9A27979B3DBB"},
        {"fruity", "BF989C52F9A74FB58A4140C426F00551"},
        {"sweet foam", "F49823EFDE55417196762A6F320273DA"}
    };

    private async Task<string> GetCoffeeBeveragePreparation(string recipeTitle)
    {
        const string coffeePreparationPattern = """<h2 class="title">\s*Preparation<\/h2>\s*<ul id=".*?">\s*(?<prepList>(.|\s)*?)\s*?<\/ul>""";
        const string instructionPattern = """<li>(?<entry>.*?)\s*?<\/li>""";
        var url = $"{_config.BaseUrl}/{recipeTitle.ToLower().Replace(" ", "-")}";
        var pageHtml = await PerformRequest(url);
        var match = Regex.Match(pageHtml, coffeePreparationPattern);
        if (match.Success)
        {
            var prepInstructionHtml = match.Groups["prepList"].Value;

            var matches = Regex.Matches(prepInstructionHtml, instructionPattern);
            if (matches.Count > 0)
            {
                var instructions = matches.Select(m => m.Groups["entry"].Value).ToList();
                return string.Join(" ", instructions);
            }
        }
        Log.Error("The coffee preparation could not be processed because the preparation list could " +
                  "not be found by the given title or pattern.");
        throw new ArgumentException("The coffee preparation could not be processed.");
    }

    private async Task<(string, string, string)> GetRandomCoffeeBeverage(List<string> filterIds)
    {
        var (title, description) = await GetRandomTeaser(filterIds);
        var preparation = await GetCoffeeBeveragePreparation(title);
        return (title, description, preparation);
    }

    private async Task<(string,string)> GetRandomTeaser(List<string> filterIds)
    {
        const string coffeeTeaserPattern = """<h2 class="title">\s*(?<title>.*?)<\/h2>\s*<p>\s*(?<description>.*?)\.\s*<\/p>""";
        var url = $"{_config.BaseUrl}?attributes=7E950F3C35AF4B84879284ED22719B58%2c{string.Join("%2c", filterIds)}";
        var pageHtml = await PerformRequest(url);
        var matches = Regex.Matches(pageHtml, coffeeTeaserPattern);

        // If no matches are found, remove the last filter and try again
        if (matches.Count == 0)
        {
            Log.Warning("No coffee recipes found for the given filters. Removing one filter and trying again.");
            if (0 < filterIds.Count)
            {
                filterIds.RemoveAt(filterIds.Count - 1);
                return await GetRandomTeaser(filterIds);
            }

            Log.Error("No coffee recipe could be found for the given filters.");
            throw new ArgumentException("No coffee recipe could be found.");
        }

        // Select a random match
        var random = new Random();
        var randomMatch = matches[random.Next(matches.Count)];
        var title = randomMatch.Groups["title"].Value
            .Replace("'", "")
            .Replace("é", "e")
            .Replace("è", "e")
            .Replace("with", "mit")
            .Replace("ë", "e")
            .Replace("Syrup", "sirup");
        var description = randomMatch.Groups["description"].Value;
        return (title, description);
    }

    private async Task<string> PerformRequest(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Send the request
            var response = await _httpClient.SendAsync(request);

            // Ensure the response was successful
            response.EnsureSuccessStatusCode();

            // Read and return the response content as a string
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Log.Error("{StatusCode}: {Message}\n{Url}", e.StatusCode, e.Message, url);

            throw e.StatusCode switch
            {
                HttpStatusCode.NotFound => new HttpRequestException("No coffee page found."),
                _ => new HttpRequestException("The coffee recipe could not be retrieved.")
            };
        }
    }

    private IEnumerable<string> MapToIds(IEnumerable<string> filters)
    {
        foreach (var filter in filters)
        {
            if (_filters.TryGetValue(filter, out var filterId))
            {
                yield return filterId;
            }
        }
    }

    private IList<string> ExtractFilters(string parameter)
    {
        const string pattern = @"['\{*](?<keyword>[^'\}]+)['\}*]";
        var match = Regex.Match(parameter, pattern);
        var filters = new List<string>();
        if (match.Success)
        {
            filters = match.Groups["keyword"].Value.Split(',').Select(filter => filter.Trim()).ToList();
        }

        return filters;
    }

    #endregion

}

[Serializable]
internal class CoffeePluginConfig
{
    public string BaseUrl { get; set; } = "https://us.jura.com/en/about-coffee/coffee-recipes";
}
