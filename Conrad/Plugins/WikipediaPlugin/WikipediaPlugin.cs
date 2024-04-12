using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;
using Exception = System.Exception;
using HtmlAgilityPack;

namespace WikipediaPlugin;

public class WikipediaPlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "WikipediaParagraphProvider";
    public string Description => "This plugin returns knowledge for a given keyword from a wikipedia entry.";
    public string ParameterFormat => "KeywordToSearch:'{keyword}'\n" +
                                     "\tA valid parameter format would be:\n" +
                                     "\tKeywordToSearch:'Google'";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        try
        {
            var keyword = ExtractKeyword(parameter);
            var responseJson =  await GetWikipediaResponse(keyword);
            string? responseHtml;
            using (JsonDocument doc = JsonDocument.Parse(responseJson))
            {
                var root = doc.RootElement;
                responseHtml = root.GetProperty("data").GetString();
            }

            if (responseHtml == null)
            {
                Log.Error("The wikipedia content could not pe parsed.");
                throw new ArgumentNullException(responseJson, "The wikipedia content could not pe parsed.");
            }

            var paragraph = ExtractParagraph(responseHtml);
            return paragraph;
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or ArgumentNullException:
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

    private string ExtractParagraph(string htmlContent)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlContent);
        const string paragraphPattern = """(?<paragraph><p>(.|\s+)*?<\/p>)""";
        var matches = Regex.Matches(htmlContent, paragraphPattern, RegexOptions.IgnoreCase);
        if (!matches.First().Groups["paragraph"].Success)
            throw new ArgumentException("No paragraph could not be extracted from the wikipedia content.");
        // Select the first <p>...</p> paragraph in the response
        htmlDoc.LoadHtml(matches.First().Groups["paragraph"].Value);
        var paragraph = htmlDoc.DocumentNode.InnerText;
        // If a <p> tag is found, return its inner text; otherwise, throw an exception
        if (paragraph != null)
            return paragraph;

        throw new ArgumentException("No paragraph could not be extracted from the wikipedia content.");

    }

    private async Task<string> GetWikipediaResponse(string keyword)
    {
        var response = string.Empty;
        try
        {
            response = await PerformRequest(keyword);
            if (response.Contains("Other reasons this message may be displayed:"))
            {
                Log.Error("The requested resource for the keyword '{Keyword}' was not found.",
                    keyword);
                throw new HttpRequestException("The requested resource was not found.", null,
                    HttpStatusCode.BadRequest);
            }
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.BadRequest)
            {
                Log.Warning("The default keyword '{Keyword}' will be used for the " +
                            "second request.", _config.DefaultKeyword);
                response = await PerformRequest(_config.DefaultKeyword);
            }
        }

        if (!response.Contains("Other reasons this message may be displayed:"))
            return response;

        Log.Error("The requested resource for the keyword '{Keyword}' was not found.",
            keyword);
        throw new HttpRequestException("The requested resource was not found.", null,
            HttpStatusCode.BadRequest);

    }

    private async Task<string> PerformRequest(string keyword)
    {
        try
        {
            var url = $"{_config.BaseUrl}?url={_config.KnowledgeUrl}{WebUtility.UrlEncode(keyword)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Set headers for this specific request
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Api-Key", _config.ApiKey);

            // Send the request
            var response = await _httpClient.SendAsync(request);

            // Ensure the response was successful
            response.EnsureSuccessStatusCode();

            // Read and return the response content as a string
            var responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }
        catch (HttpRequestException e)
        {
            Log.Error($"{e.StatusCode}: {e.Message}");

            throw e.StatusCode switch
            {
                HttpStatusCode.NotFound => new HttpRequestException("No wikipedia entry found."),
                HttpStatusCode.Unauthorized => new HttpRequestException("The API key is invalid."),
                HttpStatusCode.BadRequest => HandleBadRequest(keyword, e),
                _ => new HttpRequestException("The wikipedia information could not be retrieved.")
            };
        }
    }

    // Define this method somewhere accessible in your class
    private static HttpRequestException HandleBadRequest(string keyword, Exception innerException)
    {
        Log.Error("The article for the specified keyword '{Keyword}' could not be retrieved. " +
                  "This is probably because it is over 2 MB in size.", keyword);
        return new HttpRequestException("The article for the specified keyword could not be retrieved.", innerException, HttpStatusCode.BadRequest);
    }

    private string ExtractKeyword(string parameter)
    {
        string keyword;
        const string pattern = @"['\{*](?<keyword>[^'\}]+)['\}*]";
        char[] charsToTrim = [ '-', ':', '{', '}', '*', ',', '.', ' ', '\'', '\"' ];

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
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.api-ninjas.com/v1/webscraper";
    public string KnowledgeUrl { get; set; } = "https://en.wikipedia.org/wiki/";
    public string DefaultKeyword { get; set; } = "Observer pattern";
}
