using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;
using Exception = System.Exception;

namespace SharesPlugin;

public class SharesPlugin : IExecutorPlugin, IConfigurablePlugin
{
    #region Public

    public string Name => "SharesInformationProvider";

    public string Description => "Plugin to retrieve the last five quotes from the web for a given stock symbol.";

    public string ParameterFormat => "StockSymbol:'{symbol}'\n" +
                                     "\tThe parameter must be a string with the stock symbol. A valid parameter format " +
                                     "for the Google Inc. would be:\n" +
                                     "\tStockSymbol:'GOOG'";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the SharesPlugin");
        try
        {
            var symbol = ExtractSymbol(parameter);
            var sharesResponseString = await GetSharesInformationAsync(symbol);
            var shares = ParseAndFormatSharesResponse(sharesResponseString);
            return $"Shares Information:\n{string.Join("\n", shares)}";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or KeyNotFoundException:
                    return e.Message;
                default:
                    Log.Error($"An error occurred while retrieving the shares information:\n" +
                              $" {e.Source}: {e.Message}");
                    return "An error occurred while retrieving the shares information.";
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
        _config = configuration.Deserialize<SharesPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    #endregion

    #region Private

    private readonly HttpClient _client = new();

    private SharesPluginConfig _config = new();

    private string ExtractSymbol(string parameter)
    {
        var symbol = parameter;
        const string pattern = @"['\{](?<symbol>[^'\}]+)['\}]";
        char[] charsToTrim = ['{', '}', '(', ')', '*', ',', '.', ' ', '\''];
        var regex = new Regex(pattern);
        var match = regex.Match(parameter);

        if (match.Success)
        {
            symbol = match.Groups["symbol"].Value.Trim(charsToTrim);
        }
        else
        {
            Log.Warning("The stock symbol could not be extracted from the input parameter. The whole " +
                        "parameter will be used as the stock symbol.");
            symbol = symbol.Trim(charsToTrim);
        }
        Log.Debug("[{PluginName}] Parsed parameter: StockSymbol:'{Symbol}'",
            nameof(SharesPlugin), symbol);
        return symbol;
    }

    private IEnumerable<string> ParseAndFormatSharesResponse(string sharesResponseString)
    {
        try
        {
            using var document = JsonDocument.Parse(sharesResponseString);
            var timeSeriesElement = document.RootElement.GetProperty($"Time Series ({_config.Interval})");

            var entries = timeSeriesElement.EnumerateObject()
                .Select(entry => new
                {
                    DateTime = DateTime.ParseExact(entry.Name, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Data = entry.Value
                })
                .OrderByDescending(entry => entry.DateTime) // Ensure latest entries are first
                .Take(5) // Take only the latest five entries
                .Select(entry => new
                {
                    entry.DateTime,
                    Open = entry.Data.GetProperty("1. open").GetString(),
                    Close = entry.Data.GetProperty("4. close").GetString(),
                    Volume = entry.Data.GetProperty("5. volume").GetString()
                })
                .ToList();

            return (from entry in entries let berlinTime = entry.DateTime.AddHours(6)
                select $"\tOn {berlinTime:yyyy-MM-dd, HH:mm}: Open Price: ${entry.Open}, Close Price: " +
                       $"${entry.Close}, Trading Volume: {entry.Volume} shares").ToList();
        }
        catch (KeyNotFoundException e)
        {
            Log.Error($"The stock symbol could not be found:\n{e.Source}: {e.Message}");
            throw new KeyNotFoundException("The stock symbol could not be found.");
        }
    }

    private async Task<string> GetSharesInformationAsync(object symbol)
    {
        var url = $"{_config.BaseUrl}function=TIME_SERIES_INTRADAY&symbol={symbol}&interval={_config.Interval}&apikey={_config.ApiKey}";
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
            throw new HttpRequestException("The shares information could not be retrieved.");
        }
    }

    #endregion

}

[Serializable]
internal class SharesPluginConfig
{
    public string ApiKey { get; set; } = "";
    public string Interval { get; set; } = "60min";
    public string BaseUrl { get; set; } = "https://www.alphavantage.co/query?";
}
