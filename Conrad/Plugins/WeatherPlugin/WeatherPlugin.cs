using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using Serilog;

namespace WeatherPlugin;

public class WeatherPlugin : IExecutorPlugin, IConfigurablePlugin
{
    private HttpClient _httpClient = new();
    #region Public

    public string Name => "WeatherForecastProvider";

    public string Description =>
        "This plugin returns a weather forecast for a location, e.g. a city.";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the WeatherPlugin");
        try
        {
            var city = ExtractCity(parameter);
            var forecastString =  await GetWeatherForecastAsync(city);
            var forecast = ParseAndFormatResponse(forecastString);
            return string.Join("\n", forecast);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException:
                    Log.Error(e.Message);
                    return e.Message;
                default:
                    Log.Error("An error occured while processing the weather data:\n{Source}\n{Message}",
                        e.Source, e.Message);
                    return "An error occurred while processing the weather data.";
            }
        }
    }

    public string ParameterFormat => "ForecastCity:'{city}'";

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<WeatherPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
    }



    #endregion

    #region Private

    private WeatherPluginConfig _config = new();

    private readonly HttpClient _httpClient = new();

    public void InjectHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private string ExtractCity(string parameter)
    {
        string city;
        const string pattern = @"(?:ForecastCity:)?['\{](?<city>[^'\}]+)['\}]";
        char[] charsToTrim = ['{', '}', '*', ',', '.', ' ', '\''];

        var regex = new Regex(pattern);
        var match = regex.Match(parameter);

        if (match.Success)
        {
            city = match.Groups["city"].Value.Trim(charsToTrim);
        }
        else
        {
            Log.Warning("The city could not be extracted from the input parameter. The whole parameter will be used as the city.");
            city = parameter.Trim(charsToTrim);
        }
        Log.Debug("[{PluginName}] Parsed parameter: ForecastCity: {City}",
            nameof(WeatherPlugin), city);
        return city;
    }

    private IEnumerable<string> ParseAndFormatResponse(string forecastString)
    {
        using var doc = JsonDocument.Parse(forecastString);
        var list = doc.RootElement.GetProperty("list").EnumerateArray();

        var forecast = (from item in list
                let fullDateTimeText = item.GetProperty("dt_txt").GetString()
                let dateTime = DateTime.ParseExact(fullDateTimeText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                let dateTimeText = dateTime.ToString("yyyy-MM-dd HH:mm")
                let time = dateTime.TimeOfDay
                let temperature = item.GetProperty("main").GetProperty("temp").GetDecimal()
                let weatherDescription = item.GetProperty("weather")[0].GetProperty("description").GetString()
                let windSpeed = item.GetProperty("wind").GetProperty("speed").GetDecimal()
                where time == new TimeSpan(9, 0, 0) || time == new TimeSpan(18, 0, 0)
                select $"Weather forecast for the {dateTimeText} - A temperature of {temperature}°C with " +
                       $"{weatherDescription} and a wind speed of {windSpeed} m/s.")
            .Take(4)
            .ToList();

        return forecast;
    }

    private async Task<string> GetWeatherForecastAsync(string city)
    {
        var url = $"{_config.BaseUrl}?q={WebUtility.UrlEncode(city)}&appid={_config.ApiKey}&units={_config.Units}&cnt=20";

        var response = await _httpClient.GetAsync(url);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            Log.Error("Error while fetching weather data:\n{Source}\n{Message}",
                e.Source, e.Message);
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new HttpRequestException("The input location could not be found.");
            }
            throw new HttpRequestException("Weather data could not be fetched.");
        }

        return await response.Content.ReadAsStringAsync();
    }

    #endregion

}

[Serializable]
internal class WeatherPluginConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/forecast";
    public string Units { get; set; } = "metric";
}
