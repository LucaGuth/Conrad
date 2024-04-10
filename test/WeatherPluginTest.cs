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

[TestClass]
public class WeatherPluginTest
{
    [TestMethod]
    public async Task ExecuteTest_ValidJsonFromFile()
    {
        // Arrange
        var city = "test_city";
        var expectedString = @"Weather forecast for the 2024-04-10 18:00 - A temperature of 11,25°C with scattered clouds and a wind speed of 1,55 m/s.";
        // Load valid JSON response from file
        var jsonFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "weather_example.json"); // weather_example.json is required to be in the same directory as the test assembly
        var jsonContent = File.ReadAllText(jsonFilePath);
        var jsonResponse = JObject.Parse(jsonContent);

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
    public async Task InvalidApiKeyShouldThrowException()
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
    public async Task NetworkErrorShouldThrowException()
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
}