using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using PluginInterfaces;
using Serilog;
using Exception = System.Exception;

namespace NewsPlugin;

public class NewsPlugin : IExecutorPlugin, IConfigurablePlugin
{
    private readonly HttpClient _httpClient = new();
    private NewsPluginConfig _config = new();

    public string Name => "News Provider";
    public string Description => "This plugin returns the three latest news.";
    public string ParameterFormat => "NewsParameter:'{parameter}'\n" +
                                     "\tThe parameter must be any string. A valid parameter format would be:\n" +
                                     "\tNewsParameter:'latest'";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the NewsPlugin");

        try
        {
            var newsXml = await GetNewsAsync();
            var news = ParseAndFormatXmlResponse(newsXml);
            return $"News:\n{string.Join("\n", news)}";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or XmlException:
                    return e.Message;
                default:
                    return "An error occurred while processing the news data.";
            }
        }
    }

    private static IEnumerable<string> ParseAndFormatXmlResponse(string newsXml)
    {
        try
        {
            var doc = XDocument.Parse(newsXml);
            var items = (from item in doc.Descendants("item")
                let title = item.Element("title")?.Value
                let description = item.Element("description")?.Value
                select $"\t{title}: {description}")
                .Take(3);

            return items.ToList();
        }
        catch (Exception ex)
        {
            if (ex is not XmlException) throw;
            Log.Error($"Error parsing XML response:\n{ex.Source}\n{ex.Message}");
            throw new XmlException("The news data could not be parsed.");
        }
    }

    private async Task<string> GetNewsAsync()
    {
        var response = await _httpClient.GetAsync(_config.BaseUrl);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            Log.Error($"Error while fetching news data:\n{e.Source}\n{e.Message}");
            throw new HttpRequestException("The news data could not be fetched.");
        }

        return await response.Content.ReadAsStringAsync();
    }

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<NewsPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;
}

[Serializable]
internal class NewsPluginConfig
{
    public string BaseUrl { get; set; } = "https://www.tagesschau.de/xml/rss2";
}
