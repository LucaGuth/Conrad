namespace test;
using Newtonsoft.Json.Linq;
using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WeatherPlugin;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;


[TestClass]
public class WeatherPluginTest
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
    public async Task ValidResponseShouldBeParsed()
    {
        // Arrange
        var city = "testcity";
        var expectedString = @"Weather forecast for the 2024-04-10 18:00 - A temperature of 11 degrees celsius with scattered clouds";
        // Load valid JSON response from file
        var jsonFilePath = Path.Combine(@"..\..\..","valid_api_responses/weather_example.json");
        var jsonContent = File.ReadAllText(jsonFilePath);

        // Configure mock HTTP client to respond with the JSON loaded from the file
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.openweathermap.org/data/2.5/*")
                .Respond("application/json", jsonContent);

        var _weatherPlugin = new WeatherPlugin();
        _weatherPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        var result = await _weatherPlugin.ExecuteAsync(city);
        // Assert
        int numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.IsTrue(numLines == 4, "The result should contain 4 lines");
        Assert.IsTrue(result.Contains(expectedString), "The result should contain the expected weather forecast");
    }

    [TestMethod]
    public async Task InvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var city = "test_city";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://api.openweathermap.org/data/2.5/*")
                            .Respond(HttpStatusCode.Unauthorized); // Simulate an invalid API key by returning 401 Unauthorized

        var _weatherPlugin = new WeatherPlugin();
        _weatherPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _weatherPlugin.ExecuteAsync(city);
        if(result.Contains("Weather data could not be fetched.")){
            exceptionThrown = true;
        }
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }   

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var city = "test_city";
        
        // Simulate a network error by creating a mock HTTP client that throws an exception
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("unreachable_url"); // no response configured, will cause a network error

        var _weatherPlugin = new WeatherPlugin();
        _weatherPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _weatherPlugin.ExecuteAsync(city);
        if (result.Contains("An error occurred while processing the weather data.")){
            exceptionThrown = true;
        }
        
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for network error");
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _weatherPlugin = new WeatherPlugin();
        
        // Act
        var result = _weatherPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("BaseUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _weatherPlugin = new WeatherPlugin();
        var config = new WeatherPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _weatherPlugin.LoadConfiguration(jsonNode);

        var result = _weatherPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("TestUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _weatherPlugin = new WeatherPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _weatherPlugin.LoadConfiguration(null);

        Console.WriteLine(_weatherPlugin.GetConfigiguration());
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
internal class WeatherPluginTestConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "TestUrl";
    public string Units { get; set; } = "metric";
    public decimal WindThresholdInMS { get; set; } = 8.5m;
}