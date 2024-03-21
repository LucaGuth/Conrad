﻿using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;
using System.Text.RegularExpressions;
using System.Xml.Linq;
// ReSharper disable StringLiteralTypo

namespace BahnPlugin;

public class BahnPlugin : IExecutorPlugin, IConfigurablePlugin
{
    public string Name => "DB Train Information";
    private readonly HttpClient _client = new();

    public string Description => "This plugin returns train connection information for a given train station.";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the BahnPlugin");
        try
        {
            var parameterModel = new ParameterModel(parameter);
            var stationsStr = await GetStationsByNameAsync(parameterModel.DepartureStation);
            var root = JsonDocument.Parse(stationsStr);
            var results = root.RootElement.GetProperty("result");

            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() <= 0)
                throw new ArgumentException("The train station could not be found.");

            var timetableXml =
                await GetStationTimetableAsync(results[0].GetProperty("evaNumbers")[0].GetProperty("number").GetInt32(),
                Convert.ToDateTime(parameterModel.DepartureTime));
            var formattedConnections =
                ParseAndFormatXmlResponse(timetableXml, parameterModel.DestinationStation);
            var connections = formattedConnections as string[] ?? formattedConnections.ToArray();

            return connections.Length != 0? string.Join("\n", connections)
                : "No direct connections could be found or the destination station was not found.";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException:
                    Log.Error(e.Message);
                    return "The data for the train connection could not be retrieved.";
                case ArgumentException exception:
                    Log.Error($"ArgumentNullException: {exception.ParamName} - {exception.Message}");
                    return "The train data could not be parsed.";
                default:
                    Log.Error(e.Message);
                    return "An error occurred while processing the train data.";
            }
        }
    }

    private static IEnumerable<string> ParseAndFormatXmlResponse(string xmlResponse, string destination)
    {
        const string patternForLocation = @"\(([^)]+)\)";
        const string replacementForLocation = " $1";

        if (string.IsNullOrEmpty(xmlResponse))
            throw new ArgumentException("XML response is null or empty.", nameof(xmlResponse));

        var doc = XDocument.Parse(xmlResponse);
        if (doc.Root == null)
            throw new ArgumentNullException(nameof(xmlResponse), "XML document root is null.");

        var connections = doc.Root.Elements("s")
            .Where(s => s.Element("dp")?.Attribute("ppth")?.Value.Contains(destination) == true)
            .Select((s, index) => new
            {
                Index = index + 1,
                TrainCategory = s.Element("tl")?
                    .Attribute("c")?
                    .Value ?? throw new ArgumentNullException(nameof(xmlResponse), "Train category is null."),
                TrainNumber = s.Element("dp")?
                    .Attribute("l")?
                    .Value ?? throw new ArgumentNullException(nameof(xmlResponse), "Train number is null."),
                DepartureTime = DateTime.ParseExact(s.Element("dp")?
                    .Attribute("pt")?
                    .Value ?? throw new ArgumentNullException(nameof(xmlResponse), "Departure time is null."),
                    "yyMMddHHmm", null),
                DestinationStation = s.Element("dp")?
                    .Attribute("ppth")?.Value.Split('|')
                    .LastOrDefault()?
                    .Replace("Hbf", "Hauptbahnhof")
                                     ?? throw new ArgumentNullException(nameof(xmlResponse), "Destination station is null."),
                ViaStations = s.Element("dp")?.Attribute("ppth")?.Value.Split('|').TakeWhile(v =>
                    v != destination).Select(v =>
                {
                    v = v.Replace("Hbf", "Hauptbahnhof");
                    v = Regex.Replace(v, patternForLocation, replacementForLocation);
                    return v;
                }).ToArray() ?? throw new ArgumentNullException(nameof(xmlResponse), "Via stations are null.")
            })
            .Select(c => $"Connection {c.Index}: {c.TrainCategory} {c.TrainNumber} to " +
                         $"{c.DestinationStation}, departure on {c.DepartureTime:yy-MM-dd} at " +
                         $"{c.DepartureTime:HH:mm}{(c.ViaStations.Length != 0 ? $", via " +
                         $"{string.Join(", ", c.ViaStations)}" : " (no intermediate stop)")}.")
            .ToArray();
        return connections;
    }

    public string ParameterFormat => "DepartureStation:'{trainStation}', DestinationStation:'{trainStation}', " +
                                     "DepartureTime:'{YYYY-MM-DD HH:mm}'\n" +
                                     "When entering city names as parameters in this software application, please " +
                                     "follow the below template to ensure accurate processing:\n" +
                                     "1. General Format for Cities with Notable Features (e.g., Rivers):\n" +
                                     "\tDo not use local prepositions (e.g., \"am\", \"an der\") that are commonly " +
                                     "found in geographical names. Instead, encapsulate the distinguishing feature " +
                                     "in parentheses following the city name.\n" +
                                     "\tExample Format: City Name (Notable Feature)\n" +
                                     "\tSpecific Example: Use Frankfurt (Main) instead of Frankfurt am Main.\n" +
                                     "2. General Format for Cities with Multiple Sections or Districts:\n" +
                                     "\tUse the full name of the city, including the section or district.\n" +
                                     "\tExample Format: City Name Section\n" +
                                     "\tSpecific Example: Keep Stuttgart-Bad Cannstatt and Stuttgart Stattmitte as " +
                                     "they are.\n" +
                                     "3. Abbreviation of \"Hauptbahnhof\":\n" +
                                     "\tWhen referring to the main train station (Hauptbahnhof) within any city " +
                                     "name, abbreviate \"Hauptbahnhof\" as \"Hbf\".\n" +
                                     "\tFormat: Replace any instance of City Name Hauptbahnhof with City Name Hbf.\n" +
                                     "\tExample: Use Berlin Hbf instead of Berlin Hauptbahnhof.";

    private BahnPluginConfig _config = new();

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<BahnPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    private async Task<string> GetStationsByNameAsync(string station)
    {
        return await StationGetRequestAsync($"{_config.StationApiUrl}?searchstring=*"
                                            + $"{System.Net.WebUtility.UrlEncode(station)}*");
    }

    private async Task<string> StationGetRequestAsync(string url)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url),
            Headers =
            {
                { "DB-Client-Id", _config.ClientId },
                { "DB-Api-Key", _config.ApiKey },
                { "accept", "application/json" },
            },
        };

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> GetStationTimetableAsync(int stationNumber, DateTime departure)
        {
            var date = departure.ToString("yyMMdd"); // For the date in yyMMdd format
            var hour = departure.ToString("HH"); // For the hour in HH format
            var url = $"{_config.TimetableApiUrl}/{stationNumber}/{date}/{hour}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
                Headers =
                {
                    { "DB-Client-Id", _config.ClientId },
                    { "DB-Api-Key", _config.ApiKey },
                    { "accept", "application/xml" },
                },
            };

            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
}

[Serializable]
internal class BahnPluginConfig
{
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string StationApiUrl { get; set; } =
        "https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations";
    public string TimetableApiUrl { get; set; } =
        "https://apis.deutschebahn.com/db-api-marketplace/apis/timetables/v1/plan";
}
