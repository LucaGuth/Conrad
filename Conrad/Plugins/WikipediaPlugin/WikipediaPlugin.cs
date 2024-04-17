using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;
using Exception = System.Exception;

namespace WikipediaPlugin;

public class WikipediaPlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "WikipediaParagraphProvider";
    public string Description => "This plugin returns the first paragraph of a Wikipedia entry for a given keyword.";
    public string ParameterFormat => "KeywordToSearch:'{keyword}'\n" +
                                     "\tA valid parameter format would be:\n" +
                                     "\tKeywordToSearch:'Silicon Valley'";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        try
        {
            var keyword = ExtractKeyword(parameter);
            var pageId = await GetPageId(keyword);

            var extract =  await GetWikipediaExtract(pageId);
            var paragraphs = extract.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            if (paragraphs.Length != 0)
                return paragraphs[0];

            throw new ArgumentException("The paragraphs could not be extracted from the wikipedia content.");
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or ArgumentNullException or InvalidOperationException:
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
        _config = configuration.Deserialize<WikipediaPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
    }

    #endregion

    #region Private

    private WikipediaPluginConfig _config = new();

    private readonly HttpClient _httpClient = new();
    private static readonly char[] Separator = ['\n'];

    private async Task<int> GetPageId(string keyword)
    {
        var response = await PerformRequest($"{_config.BaseUrl}?action=query&list=search&utf8=&format=json&srsearch={WebUtility.UrlEncode(keyword)}");
        var pageId = ExtractFirstPageId(response);

        if(pageId == null )
        {
            Log.Warning("The wikipedia page for the keyword '{Keyword}' could not be retrieved. The " +
                      "default keyword '{Fallback}' will be used for the second request.", keyword,
                _config.DefaultKeyword);
            response = await PerformRequest($"{_config.BaseUrl}?action=query&list=search&utf8=&format=json&srsearch={WebUtility.UrlEncode(_config.DefaultKeyword)}");
            pageId = ExtractFirstPageId(response);
        }

        if (pageId != null)
            return (int)pageId;

        Log.Error("No wikipedia page found for the keyword '{Keyword}' and the fallback keyword '{Fallback}'.",
            keyword, _config.DefaultKeyword);
        throw new ArgumentException($"No wikipedia entry found for the keyword '{keyword}' and " +
                                    $"{_config.DefaultKeyword}.");

    }

    private int? ExtractFirstPageId(string responseJson)
    {
        int? pageId = null;
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var queryElement = root.GetProperty("query");
        var searchElement = queryElement.GetProperty("search");

        if (searchElement.GetArrayLength() > 0)
        {
            var firstResult = searchElement[0];
            pageId = firstResult.GetProperty("pageid").GetInt32();
        }

        return pageId;
    }

    private async Task<string> GetWikipediaExtract(int pageId)
    {
        var url = $"{_config.BaseUrl}?action=query&format=json&prop=extracts&exlimit=1&explaintext=1&pageids=";

        var response = await PerformRequest(url + pageId);
        var extract = ExtractExtract(response);

        if (!string.IsNullOrEmpty(extract))
            return extract;

        throw new ArgumentException($"The wikipedia content for the page '{pageId}' could not be retrieved.");
    }

    private string? ExtractExtract(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var queryElement = root.GetProperty("query");
        var pagesElement = queryElement.GetProperty("pages");

        // Iterate over the properties in "pages" object since page ID is dynamic
        var page = pagesElement.EnumerateObject().FirstOrDefault();
        // We assume there's only one page object here, but you could handle multiple pages differently
        return page.Value.GetProperty("extract").GetString();
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
            Log.Error($"{e.StatusCode}: {e.Message}");

            throw e.StatusCode switch
            {
                HttpStatusCode.NotFound => new HttpRequestException("No wikipedia entry found."),
                HttpStatusCode.Unauthorized => new HttpRequestException("The API key is invalid."),
                _ => new HttpRequestException("The wikipedia information could not be retrieved.")
            };
        }
    }

    private string ExtractKeyword(string parameter)
    {
        string keyword;
        const string pattern = @"['\{*](?<keyword>[^'\}]+)['\}*]";
        char[] charsToTrim = [ '-', ':', '{', '}', '*', ',', ' ', '\'', '\"' ];

        var regex = new Regex(pattern);
        var match = regex.Match(parameter);

        if (match.Success)
        {
            keyword = match.Groups["keyword"].Value.Trim(charsToTrim);
        }
        else
        {
            Log.Warning("The keyword could not be extracted from the input parameter '{Parameter}'. The whole parameter will be used as the keyword.", parameter);
            keyword = parameter.Trim(charsToTrim);
        }
        Log.Debug("[{PluginName}] Parsed parameter: KeywordToSearch:'{Keyword}'",
            nameof(WikipediaPlugin), keyword);
        return keyword;
    }

    #endregion

}

[Serializable]
internal class WikipediaPluginConfig
{
    public string BaseUrl { get; set; } = "https://en.wikipedia.org/w/api.php";
    public string DefaultKeyword { get; set; } = "Observer pattern";
}
