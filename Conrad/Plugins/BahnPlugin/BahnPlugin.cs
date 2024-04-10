using System.Globalization;
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
    #region Public

    public string Name => "DB Train Information Provider";

    public string Description => "This plugin provides train connection information. Train " +
                                 "connections with intermediate stops for changing trains cannot be called up. Only " +
                                 "train connections that have not yet left the departure station can be retrieved.";
    public string ParameterFormat =>
        "DepartureStation:'{trainStation}', DestinationStation:'{trainStation}', " +
        "DepartureTime:'{yyyy-MM-dd HH:mm}'\n" +
        $"\tThe departure time must be between {DateTime.Now:yyyy-MM-dd HH:mm} and {DateTime.Now.AddHours(18):yyyy-MM-dd HH:mm}" +
        $"\tA valid example would be: DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:'{DateTime.Now.AddHours(5):yyyy-MM-dd HH:mm}'";

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
            var allConnections = await ProcessStations(results, parameterModel);

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
                case ArgumentException or ArgumentNullException or HttpRequestException:
                    return e.Message;
                default:
                    Log.Error($"{e.Source}\n{e.Message}");
                    return "An error occurred while processing the train data.";
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
        _config = configuration.Deserialize<BahnPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;

    #endregion

    #region Private

    private readonly HttpClient _client = new();

    private BahnPluginConfig _config = new();

    private async Task<IList<string>> ProcessStations(JsonElement results, ParameterModel parameterModel)
    {
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

        return allConnections;
    }

    private IEnumerable<string> ParseAndFormatXmlResponse(string xmlResponse, string destination)
    {
        ValidateXmlResponse(xmlResponse);

        var doc = XDocument.Parse(xmlResponse);
        ValidateDocumentRoot(doc);

        destination = PreprocessDestination(destination);

        var connections = ExtractConnections(doc, destination);

        return FormatConnections(connections);
    }

    private void ValidateXmlResponse(string xmlResponse)
    {
        if (!string.IsNullOrEmpty(xmlResponse)) return;
        Log.Error($"{nameof(xmlResponse)} - XML response is null or empty.");
        throw new ArgumentException("The timetable data could not be parsed.");
    }

    private static void ValidateDocumentRoot(XDocument doc)
    {
        if (doc.Root != null) return;
        Log.Error("XML document root is null.");
        throw new ArgumentNullException(nameof(doc.Root), "The timetable data could not be parsed.");
    }

    private string PreprocessDestination(string destination)
    {
        var regex = new Regex(@"\s+\(", RegexOptions.Compiled);
        return regex.Replace(destination, "(");
    }

    private IEnumerable<dynamic> ExtractConnections(XContainer docRoot, string destination)
    {
        return docRoot.Elements("s")
            .Where(s => s.Element("dp")?.Attribute("ppth")?.Value.Contains(destination) == true)
            .Select(s => ParseConnection(s, destination)).ToArray();
    }

    private dynamic ParseConnection(XElement s, string destination)
    {
        var trainNumber = ExtractTrainNumber(s);
        var trainCategory = GetAttributeValueOrThrow(s.Element("tl"), new[] { "c" }, "Train category could not be parsed.");
        var departureTimeString = GetAttributeValueOrThrow(s.Element("dp"), new[] { "pt" }, "Departure time could not be parsed.");
        var departureTime = DateTime.ParseExact(departureTimeString, "yyMMddHHmm", CultureInfo.InvariantCulture);

        var destinationStation = ProcessDestinationStation(s);

        var viaStations = ProcessViaStations(s, destination);

        return new
        {
            TrainCategory = trainCategory,
            TrainNumber = trainNumber,
            DepartureTime = departureTime,
            DestinationStation = destinationStation,
            ViaStations = viaStations
        };
    }

    private string ExtractTrainNumber(XElement s)
    {
        // For IC, ICE, EC, TGV the train number is not in the "l" attribute
        return s.Element("dp")?.Attribute("l")?.Value
               ?? GetAttributeValueOrThrow(s.Element("tl"), new[] { "n" }, "Train number could not be parsed.");
    }

    private string ProcessDestinationStation(XElement s)
    {
        var destinationStationRaw = s.Element("dp")?.Attribute("ppth")?.Value ?? throw new ArgumentNullException(nameof(s), "Destination station could not be parsed.");
        return destinationStationRaw.Split('|').LastOrDefault()?.Replace("Hbf", "Hauptbahnhof")
               ?? throw new ArgumentNullException(nameof(s), "Destination station could not be parsed after processing.");
    }

    private IEnumerable<string> ProcessViaStations(XElement s, string destination)
    {
        const string patternForLocation = @"\(([^)]+)\)";
        const string replacementForLocation = " $1";

        var destinationStationRaw = s.Element("dp")?.Attribute("ppth")?.Value;
        return destinationStationRaw?.Split('|').TakeWhile(v => v != destination).Select(v =>
        {
            v = v.Replace("Hbf", "Hauptbahnhof");
            return Regex.Replace(v, patternForLocation, replacementForLocation, RegexOptions.Compiled);
        }) ?? Enumerable.Empty<string>();
    }

    private string GetAttributeValueOrThrow(XElement? element, string[] attributeNames, string exceptionMessage)
    {
        foreach (var name in attributeNames)
        {
            var value = element?.Attribute(name)?.Value;
            if (!string.IsNullOrEmpty(value)) return value;
        }

        Log.Error(exceptionMessage);
        throw new ArgumentNullException(nameof(element), "The timetable data could not be parsed.");
    }

    private IEnumerable<string> FormatConnections(IEnumerable<dynamic> connections)
    {
        return connections.Select(c => $"{c.TrainCategory} {c.TrainNumber} to " +
                                       $"{c.DestinationStation}, departure on {c.DepartureTime:yy-MM-dd} at " +
                                       $"{c.DepartureTime:HH:mm}{(c.ViaStations.Any() ? $", via " + string.Join(", ", c.ViaStations) : " (no intermediate stop)")}")
                          .ToArray();
    }

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
    #endregion
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
