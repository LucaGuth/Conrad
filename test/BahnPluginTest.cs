namespace test; 

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using BahnPlugin; 
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;


[TestClass]
public class BahnPluginTest
{
    [TestInitialize]
    public void TestInit()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }
    
    [TestCleanup]
    public void TestDispose()
    {
        Log.CloseAndFlush();
    }

    [TestMethod]
    public async Task ValidResponse()
    {
        // Arrange
        var parameter = "DepartureStation:'Sulzbach an der Murr', DestinationStation:'Stuttgart', DepartureTime:'2024-04-11 16:00";
        var expectedString = @"Connection 1: RE 90 to Stuttgart Hauptbahnhof, departure on 24-04-11 at 16:25, via Oppenweiler Württ, Backnang, Winnenden, Waiblingen, Stuttgart-Bad Cannstatt, Stuttgart Hauptbahnhof";

        // Load Json and Xml responses from files
        var resourcePath = Path.Combine(@"..\..\..", "valid_api_responses");
        var station1JsonFilePath = Path.Combine(resourcePath, "bahn_example_station_1.json");
        var station1JsonContent = File.ReadAllText(station1JsonFilePath);
        var station1JsonResponse = JObject.Parse(station1JsonContent);
        var station2JsonFilePath = Path.Combine(resourcePath, "bahn_example_station_2.json");
        var station2JsonContent = File.ReadAllText(station2JsonFilePath);
        var station2JsonResponse = JObject.Parse(station2JsonContent);
        var timetableXmlFilePath = Path.Combine(resourcePath, "bahn_example_timetable.xml");
        var timetableXmlContent = File.ReadAllText(timetableXmlFilePath);
        var timetableXml = XDocument.Parse(timetableXmlContent);
        // Create a mock HTTP client that returns the JSON and XML responses
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations?searchstring=*Sulzbach+(Murr)*")
                .Respond("application/json", station1JsonContent);
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations?searchstring=*Stuttgart*")
                .Respond("application/json", station2JsonContent);
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/timetables/v1/plan*")
                .Respond("application/xml", timetableXmlContent);
        
        var _bahnPlugin = new BahnPlugin();
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        var numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.IsTrue(numLines == 2, "The result should contain 2 lines");
        Assert.IsTrue(result.Contains(expectedString) , "The result should contain the expected train information");
        
    }   


    [TestMethod]
    public async Task InvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:2024-11-04 10:37";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations*") // Adjusted API URL
                            .Respond(HttpStatusCode.Unauthorized); // Simulate an invalid API key by returning 401 Unauthorized

        var _bahnPlugin = new BahnPlugin(); // Adjusted plugin name
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        if(result.Contains("An error occurred while retrieving the data. Please check the internet connection.")){
            exceptionThrown = true;
        }
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }   

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:2024-11-04 10:37";
        
        // Simulate a network error by creating a mock HTTP client that throws an exception
        var mockHttp = new MockHttpMessageHandler(); // no response configured, will cause a network error

        var _bahnPlugin = new BahnPlugin();
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        if (result.Contains("An error occurred while processing the train data.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);  
        
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for network error");
    }

    [TestMethod]
    public async Task StationIsInvalidShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:2024-11-04 10:37"; // valid input parameter
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations*")
                        .Respond(HttpStatusCode.BadRequest); // Simulate a station invalid by returning 400 Bad Request

        var _bahnPlugin = new BahnPlugin();
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        if (result.Contains("The station name: Stuttgart is invalid.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for bad request");
    }

    [TestMethod]
    public async Task StationNotFoundShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:2024-11-04 10:37"; // valid input parameter
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations*")
                        .Respond(HttpStatusCode.NotFound); // Simulate a station cant be found by returning 404 Not Found

        var _bahnPlugin = new BahnPlugin();
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        if (result.Contains("The station: Stuttgart could not be found.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for resourece not found");
    }

    [TestMethod]
    public async Task TimetableGoneShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Sulzbach an der Murr', DestinationStation:'Stuttgart', DepartureTime:'2024-04-11 16:00"; // valid input parameter
        // Load Json responses from files
        var resourcePath = Path.Combine(@"..\..\..", "valid_api_responses");
        var station1JsonFilePath = Path.Combine(resourcePath, "bahn_example_station_1.json");
        var station1JsonContent = File.ReadAllText(station1JsonFilePath);
        var station1JsonResponse = JObject.Parse(station1JsonContent);
        var station2JsonFilePath = Path.Combine(resourcePath, "bahn_example_station_2.json");
        var station2JsonContent = File.ReadAllText(station2JsonFilePath);
        var station2JsonResponse = JObject.Parse(station2JsonContent);
        // Create a mock HTTP client that returns the JSON responses
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations?searchstring=*Sulzbach+(Murr)*")
                .Respond("application/json", station1JsonContent);
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/station-data/v2/stations?searchstring=*Stuttgart*")
                .Respond("application/json", station2JsonContent);
        mockHttp.When("https://apis.deutschebahn.com/db-api-marketplace/apis/timetables/v1/plan*")
                .Respond(HttpStatusCode.Gone, "application/xml", "The timetable is no longer available.");
        
        var _bahnPlugin = new BahnPlugin();
        _bahnPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);
        if (result.Contains("The timetable is no longer available.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for gone");
    }
    

    [TestMethod]
    public async Task UnparsableInputParameterShouldBeCaught()
    {
        // Arrange
        var parameter = "unparsable_parameter"; 
        var _bahnPlugin = new BahnPlugin();

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);

        // Assert
        if (result.Contains("DepartureStation and DestinationStation could not be parsed from the input parameter.")){
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for unparsable input parameter");
    }

    [TestMethod]
    public async Task ParsableInputParameterShouldBeHandled()
    {
        // Arrange
        var parameter = "DepartureStation:'Stuttgart', DestinationStation:'München', DepartureTime:2024-11-04 10:37";
        var _bahnPlugin = new BahnPlugin();

        // Act
        var exceptionThrown = false;
        var result = await _bahnPlugin.ExecuteAsync(parameter);

        // Assert
        if (result.Contains("DepartureStation and DestinationStation could not be parsed from the input parameter.")){
            exceptionThrown = true;
        }
        Assert.IsFalse(exceptionThrown, "An exception should not have been thrown for parsable input parameter");
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _bahnPlugin = new BahnPlugin();
        
        // Act
        var result = _bahnPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("ClientId"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _bahnPlugin = new BahnPlugin();
        var config = new BahnPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _bahnPlugin.LoadConfiguration(jsonNode);

        var result = _bahnPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("TestUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _bahnPlugin = new BahnPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _bahnPlugin.LoadConfiguration(null);

        Console.WriteLine(_bahnPlugin.GetConfigiguration());
        }
        catch (Exception e)
        {
            exceptionThrown = true;
        }
        // Assert
        Assert.IsTrue(exceptionThrown);
    }
}

[Serializable]
    internal class BahnPluginTestConfig
    {
        public string ClientId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string StationApiUrl { get; set; } =
            "TestUrl";
        public string TimetableApiUrl { get; set; } =
            "TestUrl";
    }