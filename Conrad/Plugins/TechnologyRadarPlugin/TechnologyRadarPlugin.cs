using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PluginInterfaces;
using Serilog;

namespace TechnologyRadarPlugin;

public class TechnologyRadarPlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "TechnologyTrendsInformer";

    public string Description => "This plugin returns the latest technology trends for the IT sector.";

    public string ParameterFormat => "TechnologyTrendsInformer:'{parameter}'\n" +
                                     "\tThe parameter must be any string.";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        try
        {
            var (category, pageString) = await GetHtmlPageAsync();
            var (title, paragraph) = ExtractRandomParagraph(pageString);
            return $"Technology trend in the category '{category}': {title} - {paragraph} (Source: ThoughtWorks)";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException:
                    return e.Message;
                default:
                    Log.Error($"An error has occurred while processing the Technology Trend information:\n" +
                              $" {e.Source}: {e.Message}");
                    return "An error has occurred while processing the Technology Trend information.";
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
        _config = configuration.Deserialize<TechnologyRadarPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    #endregion

    #region Private

    private readonly HttpClient _httpClient = new();

    private TechnologyRadarPluginConfig _config = new();

    private (string title, string paragraph) ExtractRandomParagraph(string pageString)
    {
        const string paragraphPattern = """<div class="cmp-blip-accordion__list--description" id="blip-description-\d*?">\s*(?<paragraph>.*?)\s*?<\/div>""";
        const string titlePattern = """<strong>(?<title>(.|\s)*?)<\/strong>""";
        var paragraphMatches = Regex.Matches(pageString, paragraphPattern);

        var random = new Random();
        var randomIndex = random.Next(paragraphMatches.Count);
        var randomParagraphHtml = paragraphMatches[randomIndex].Groups["paragraph"].Value;
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(randomParagraphHtml);
        var paragraph = htmlDoc.DocumentNode.InnerText.Trim();

        var titleMatch =  Regex.Match(randomParagraphHtml, titlePattern);
        var titleHtml = titleMatch.Groups["title"].Value;
        htmlDoc.LoadHtml(titleHtml);
        var title = htmlDoc.DocumentNode.InnerText.Trim();

        return (title, paragraph);
    }

    private async Task<(string,string)> GetHtmlPageAsync()
    {
        try {
            var random = new Random();
            var randomIndex = random.Next(_config.TechnologyTrendUrlExtensions.Length);
            var randomUrlExtension = _config.TechnologyTrendUrlExtensions[randomIndex];

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}{randomUrlExtension}");
            response.EnsureSuccessStatusCode();

            return (randomUrlExtension, await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException e)
        {
            Log.Error($"{e.StatusCode}: {e.Message}");
            throw new HttpRequestException("The information for the technology trend could not be " +
                                           "retrieved. Please check your internet connection and the service " +
                                           "availability with the configured URL.");
        }
    }

    #endregion
}

[Serializable]
internal class TechnologyRadarPluginConfig
{
    public string BaseUrl { get; set; } = "https://www.thoughtworks.com/de-de/radar/";

    public string[] TechnologyTrendUrlExtensions { get; set; } =
    [
        "techniques/",
        "tools/",
        "platforms/",
        "languages-and-frameworks/"
    ];
}
