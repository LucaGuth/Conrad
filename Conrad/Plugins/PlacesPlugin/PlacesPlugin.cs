using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;
using static System.Text.RegularExpressions.Regex;

namespace PlacesPlugin;

public class PlacesPlugin : IConfigurablePlugin, IExecutorPlugin
{
    #region Public

    public string Name => "RestaurantsProvider";
    public string Description => $"This plugin returns the three nearest restaurants based on a location " +
                                 $"as parameter.";
    public string ParameterFormat => "Location:'{location}'\n\t" +
                                     "The location parameter is required and must be a string. A valid parameter " +
                                     "format would be:\n" +
                                     "\tLocation:'Lerchenstraße 1, 70174 Stuttgart'";

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the PlacesPlugin");

        try {
            var address = ExtractLocation(parameter);
            var (lat, lng) = await GeocodeAddress(address);
            var responseJson = await FindNearbyRestaurants(lat, lng);
            // Parse the JSON response and extract the top 3 restaurants
            var restaurants = ParseRestaurants(responseJson).Take(3);

            // Format the restaurants into a string
            return FormatRestaurants(restaurants);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException or ArgumentException:
                    return e.Message;
                default:
                    Log.Error("An error has occurred while retrieving the restaurant information:\n" +
                              " {Source}: {Message}\n{StackTrace}", e.Source, e.Message,
                        e.StackTrace);
                    return "An error has occurred while retrieving the restaurant information.";
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
        _config = configuration.Deserialize<PlacesPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    #endregion

    #region Private

        private PlacesPluginConfig _config = new();

        private readonly HttpClient _client = new();

        private static IEnumerable<Restaurant> ParseRestaurants(string jsonResponse)
    {
        using var doc = JsonDocument.Parse(jsonResponse);
        var results = doc.RootElement.GetProperty("results");
        var restaurants = new List<Restaurant>();
        foreach (var result in results.EnumerateArray())
        {
            var name = result.GetProperty("name").GetString();
            var vicinity = result.GetProperty("vicinity").GetString();
            var rating = result.GetProperty("rating").GetDouble();
            if (name != null && vicinity != null)
            {
                restaurants.Add(new Restaurant
                {
                    Name = name,
                    Address = vicinity,
                    Rating = rating
                });
            }
            else
            {
                Log.Error("The response of the restaurants request could not be parsed.\n{Response}",
                    jsonResponse);
                throw new ArgumentException("The restaurant information could not be processed.");
            }
        }
        return restaurants;
    }

    private static string FormatRestaurants(IEnumerable<Restaurant> restaurants)
    {
        var formatted = new StringBuilder();
        var count = 1;
        foreach (var restaurant in restaurants)
        {
            formatted.AppendLine($"\tRestaurant {count}: {restaurant.Name}, " +
                                 $"Address: {restaurant.Address.Replace(",", " in")}, " +
                                 $"Rating: {restaurant.Rating}/5");
            count++;
        }

        // Check if StringBuilder is not empty and remove the last character (newline)
        if (formatted.Length > 0)
        {
            formatted.Length--;
        }

        return $"Nearby Restaurant Information:\n{formatted}";
    }

    private string ExtractLocation(string parameter)
    {
        var address = parameter;
        const string pattern = @"(?:Location:)?['\{](?<location>[^'\}]+)['\}]";
        char[] charsToTrim = ['{', '}', '*', ',', '.', ' ', '\''];
        var match = Match(parameter, pattern);

        if (match.Success)
        {
            address =  match.Groups[1].Value.Trim(charsToTrim);
        }
        else
        {
            // If the pattern is not matched, log the warning and return the default parameter
            Log.Warning("The input parameter: {Parameter} does not match the required format for the" +
                        " location. The whole parameter will be used as location", parameter);
            address = address.Trim(charsToTrim);
        }

        Log.Debug("[{PluginName}] Parsed parameter: Location:'{Address}'",
            nameof(PlacesPlugin), address);
        return address;
    }

    private async Task<(double, double)> GeocodeAddress(string address)
    {
        var requestUri = $"{_config.GeoCodeUrl}?address={Uri.EscapeDataString(address)}&key={_config.ApiKey}";

        var response = await PerformRequest(requestUri);
        var jsonDoc = JsonDocument.Parse(response);
        var location = jsonDoc.RootElement.GetProperty("results")[0].GetProperty("geometry")
            .GetProperty("location");
        var lat = location.GetProperty("lat").GetDouble();
        var lng = location.GetProperty("lng").GetDouble();
        return (lat, lng);
    }

    private async Task<string> FindNearbyRestaurants(double latitude, double longitude)
    {
        var requestUri =
            $"{_config.NearbySearchUrl}?location={latitude.ToString(CultureInfo.InvariantCulture)}," +
            $"{longitude.ToString(CultureInfo.InvariantCulture)}&radius={_config.Radius}&type=restaurant&key=" +
            $"{_config.ApiKey}";
        return await PerformRequest(requestUri);
    }

    private async Task<string> PerformRequest(string url)
    {
        try
        {
            var response = await _client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
            // Parse the JSON response to check the "status" field
            var jsonResponse = JsonDocument.Parse(responseString);
            var status = jsonResponse.RootElement.GetProperty("status").GetString();

            switch (status)
            {
                case "OK":
                    return responseString; // Request successful, return the response string
                case "ZERO_RESULTS":
                    Log.Warning("No restaurants found near the location.");
                    throw new ArgumentException("No restaurants found near the location.");
                case "REQUEST_DENIED":
                    Log.Error("The request was denied. Check the credentials in the config.");
                    throw new HttpRequestException("The request for the restaurants was denied. Check the " +
                                                   "credentials in the config.");
                default:
                    // Handle specific statuses you expect from the API.
                    // For example, INVALID_REQUEST, etc.
                    var errorMessage = $"API request failed with status: {status}";
                    Log.Error(errorMessage);
                    throw new HttpRequestException("The restaurants information could not be retrieved.");
            }
        }
        catch (Exception e)
        {
            switch (e)
            {
                // This catch block is for handling HTTP request errors which are not thrown by the default case of the
                case HttpRequestException exception:
                    if (exception.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        Log.Error("Unauthorized access to the API. Check the API key in the configuration.");
                        throw new HttpRequestException("Unauthorized access to the API. Check the API key in the configuration.");
                    }
                    if (exception.Source == nameof(PlacesPlugin))
                    {
                        throw;
                    }
                    Log.Error("Network error:\n{Source}\n{Message}", exception.Source, exception.Message);
                    throw new HttpRequestException(
                        "Failed to retrieve the restaurant information probably due to a network error.");
                case JsonException:
                    // This catch block is for handling JSON parsing errors
                    Log.Error("JSON parsing error:\n{Source}\n{Message}", e.Source, e.Message);
                    throw new HttpRequestException("Failed to process the restaurant data.");
                default:
                    throw;
            }
        }
    }

    #endregion

}

[Serializable]
internal class PlacesPluginConfig
{
    public string ApiKey { get; set; } = "";
    public string NearbySearchUrl { get; set; } = "https://maps.googleapis.com/maps/api/place/nearbysearch/json";
    public string GeoCodeUrl { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";
    public string Radius { get; set; } = "1500";
}
