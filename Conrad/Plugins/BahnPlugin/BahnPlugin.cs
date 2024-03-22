using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using PluginInterfaces;
using Serilog;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Exception = System.Exception;

// ReSharper disable StringLiteralTypo

namespace BahnPlugin;

public class BahnPlugin : IExecutorPlugin, IConfigurablePlugin
{
    public string Name => "DB Train Information";
    private readonly HttpClient _client = new();

    public string Description => "This plugin provides train connection information between two stations. Train " +
                                 "connections with intermediate stops for changing trains cannot be called up. Only " +
                                 "train connections that have not yet left the departure station can be retrieved.";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Start execution of the BahnPlugin");

        try
        {
            var parameterModel = new ParameterModel(parameter);
            var departureStationStr = await GetStationsByNameAsync(parameterModel.DepartureStation);
            _ = await GetStationsByNameAsync(parameterModel.DestinationStation);
            var root = JsonDocument.Parse(departureStationStr);
            var results = root.RootElement.GetProperty("result");

            // Check if there are any stations to process
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() <= 0)
                throw new ArgumentException("The train station could not be found.");

            // Prepare a list to hold all connections from all evaNumbers
            var allConnections = new List<string>();

            // Iterate through each result to process its evaNumbers
            foreach (var evaNumber in
                     from result in results.EnumerateArray()
                     select result.GetProperty("evaNumbers").EnumerateArray()
                     into evaNumbers
                     from evaNumberElement in evaNumbers
                     select evaNumberElement.GetProperty("number").GetInt32())
            {
                // Fetch the timetable XML for each evaNumber
                var timetableXml =
                    await GetStationTimetableAsync(evaNumber, Convert.ToDateTime(parameterModel.DepartureTime));
                // Parse the fetched timetable XML to get formatted connections
                var formattedConnections =
                    ParseAndFormatXmlResponse(timetableXml, parameterModel.DestinationStation);

                // If formattedConnections is not directly an array, ensure it's treated as one
                var connections = formattedConnections as string[] ?? formattedConnections.ToArray();

                // Add the fetched connections to the allConnections list
                allConnections.AddRange(connections);
            }

            // Build the final result string from all accumulated connections
            return allConnections.Count != 0
                ? string.Join("\n", allConnections.Select((connection, index)
                    => $"Connection {index + 1}: {connection}"))
                : "No direct connections could be found.";
        }
        catch (Exception e)
        {
            switch (e)
            {
                case ArgumentException or HttpRequestException:
                    return e.Message;
                default:
                    Log.Error($"{e.Source}\n{e.Message}");
                    return "An error occurred while processing the train data.";
            }
        }
    }

    private static IEnumerable<string> ParseAndFormatXmlResponse(string xmlResponse, string destination)
    {
        const string patternForLocation = @"\(([^)]+)\)";
        const string replacementForLocation = " $1";
        const string cantParseMessage = "The timetable data could not be parsed.";
        var regex = new Regex(@"\s+\(", RegexOptions.Compiled);
        destination = regex.Replace(destination, "(");

        if (string.IsNullOrEmpty(xmlResponse))
        {
            Log.Error($"{nameof(xmlResponse)} - XML response is null or empty.");
            throw new ArgumentException(cantParseMessage);
        }

        var doc = XDocument.Parse(xmlResponse);
        if (doc.Root == null)
        {
            Log.Error($"{nameof(xmlResponse)} - XML document root is null.");
            throw new ArgumentNullException(nameof(xmlResponse), cantParseMessage);
        }

        var connections = doc.Root.Elements("s")
            .Where(s => s.Element("dp")?.Attribute("ppth")?.Value.Contains(destination) == true)
            .Select((s) =>
            {
                // Using the local function
                var trainCategory = GetAttributeValueOrThrow(s.Element("tl"), new[] { "c" }, "Train category could not be parsed.");
                var trainNumber = GetAttributeValueOrThrow(s.Element("dp"), new[] { "l", "n" }, "Train number could not be parsed.");
                var departureTimeString = GetAttributeValueOrThrow(s.Element("dp"), new[] { "pt" }, "Departure time could not be parsed.");
                var departureTime = DateTime.ParseExact(departureTimeString, "yyMMddHHmm", null);

                // Processing DestinationStation using a modified version of the local function
                var destinationStationRaw = s.Element("dp")?.Attribute("ppth")?.Value ?? throw new ArgumentNullException(nameof(xmlResponse), "Destination station could not be parsed.");
                var destinationStation = destinationStationRaw.Split('|').LastOrDefault()?.Replace("Hbf", "Hauptbahnhof")
                                         ?? throw new ArgumentNullException(nameof(xmlResponse), "Destination station could not be parsed after processing.");

                // Processing for ViaStations with additional logic
                var viaStations = destinationStationRaw.Split('|').TakeWhile(v => v != destination).Select(v =>
                {
                    v = v.Replace("Hbf", "Hauptbahnhof");
                    v = Regex.Replace(v, patternForLocation, replacementForLocation, RegexOptions.Compiled);
                    return v;
                }).ToArray();

                return new
                {
                    TrainCategory = trainCategory,
                    TrainNumber = trainNumber,
                    DepartureTime = departureTime,
                    DestinationStation = destinationStation,
                    ViaStations = viaStations
                };

                // Local function for extracting attribute value or throwing exception
                string GetAttributeValueOrThrow(XElement? element, IEnumerable<string> attributeNames, string exceptionMessage)
                {
                    foreach (var name in attributeNames)
                    {
                        var value = element?.Attribute(name)?.Value;
                        if (!string.IsNullOrEmpty(value)) return value;
                    }

                    Log.Error(exceptionMessage);
                    throw new ArgumentNullException(cantParseMessage);
                }
            }).ToArray()
            .Select(c => $"{c.TrainCategory} {c.TrainNumber} to " +
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
        try {
            return await StationGetRequestAsync($"{_config.StationApiUrl}?searchstring=*"
                                            + $"{WebUtility.UrlEncode(station)}*");
        }
        catch(HttpRequestException e)
        {
            Log.Error(e.Message);
            throw e.StatusCode switch
            {
                (HttpStatusCode.NotFound) => new HttpRequestException($"The station: {station} could not be found."),
                (HttpStatusCode.BadRequest) => new HttpRequestException($"The station name: {station} is invalid."),
                _ => new HttpRequestException("An error occurred while retrieving the data. Please check the internet connection.")
            };
        }
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
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Log.Error(e.Message);
                if (e.StatusCode == HttpStatusCode.Gone)
                {
                    throw new HttpRequestException("The timetable is no longer available.");
                }
                throw new HttpRequestException("The timetable for the station could not be found.");
            }
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
