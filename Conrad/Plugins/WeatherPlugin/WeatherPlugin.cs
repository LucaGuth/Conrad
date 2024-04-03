using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CSharp.RuntimeBinder;
using PluginInterfaces;
using Serilog;
using WeatherPlugin.PluginModels;

namespace WeatherPlugin;

public class WeatherPlugin : IExecutorPlugin, IConfigurablePlugin
{
    private readonly HttpClient _httpClient = new();

    public string Name => "Weather Forecast";
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public string Description =>
        "This plugin returns a weather forecast for a period of time. The time span ranges from the current date to" +
        "a maximum of five days in the future. A city must be specified as the location for the weather forecast.";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the WeatherPlugin");
        try
        {
            var parameterModel = new ParameterModel(parameter);
            var forecastString =  await GetWeatherForecastAsync(parameterModel.City);
            var forecastResponse = JsonSerializer.Deserialize<WeatherForecast>(forecastString, _options) ?? throw new ArgumentException("The weather data could not be parsed.");
            var forecast = new WeatherForecast();
            const string format = "yyyy-MM-dd HH:mm:ss";
            foreach (var forecastListItem in forecastResponse.List)
            {
                if (DateTime.TryParseExact(forecastListItem.Dt_Txt, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                {
                    if (parameterModel.ForecastFromDate <= dateTime && dateTime <= parameterModel.ForecastUntilDate)
                    {
                        forecast.List.Add(forecastListItem);
                    }
                }
                else
                {
                    throw new ArgumentException("The weather data could not be parsed.");
                }
            }

            forecast.City = forecastResponse.City;

            return JsonSerializer.Serialize(forecast);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case ArgumentException or HttpRequestException:
                    Log.Error(e.Message);
                    return e.Message;
                case RuntimeBinderException:
                    const string errorMsg = "The weather data could not be parsed.";
                    Log.Error(errorMsg);
                    return errorMsg;
                default:
                    Log.Error(e.Message);
                    return string.Empty;
            }
        }
    }

    public string ParameterFormat =>
        "ForecastFromDate:'{YYYY-MM-DD}', ForecastUntilDate:'{YYYY-MM-DD}', " +
        "ForecastCity:'{city}'";

    private WeatherPluginConfig _config = new();
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

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    private async Task<string> GetWeatherForecastAsync(string city)
    {
        var url = $"{_config.BaseUrl}?q={WebUtility.UrlEncode(city)}&appid={_config.ApiKey}&units={_config.Units}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

[Serializable]
internal class WeatherPluginConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/forecast";
    public string Units { get; set; } = "metric";
}
