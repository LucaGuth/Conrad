namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using PlacesPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;


[TestClass]
public class PlacesPluginTest
{
    [TestMethod]
    public async Task ValidResponseShouldBeParsed()
    {
        // Arrange
        var parameter = "Lerchenstraße 1, 70174 Stuttgart";
        var expectedString = "Restaurant 1: Hotel Royal, Address: Sophienstraße 35 in Stuttgart, Rating: 4/5";

        var jsonFilePath1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "places_example_1.json"); // places_example.json is required to be in the same directory as the test assembly
        var jsonContent1 = File.ReadAllText(jsonFilePath1);
        var jsonResponse1 = JObject.Parse(jsonContent1);
        
        var jsonFilePath2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "places_example_2.json"); // places_example.json is required to be in the same directory as the test assembly
        var jsonContent2 = File.ReadAllText(jsonFilePath2);
        var jsonResponse2 = JObject.Parse(jsonContent2);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://maps.googleapis.com/maps/api/geocode/json*")
            .Respond("application/json", jsonContent1);
        mockHttp.When("https://maps.googleapis.com/maps/api/place/nearbysearch/json*")
            .Respond("application/json", jsonContent2);

        var _placesPlugin = new PlacesPlugin();
        _placesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _placesPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        
        // Assert
        var numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.IsTrue(numLines == 4, "The result should contain 4 lines");
        Assert.IsTrue(result.Contains(expectedString), "The result should contain the expected restaurant information");

    }

    [TestMethod]
    public async Task NearbySearchInvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var parameter = "Lerchenstraße 1, 70174 Stuttgart";

        var jsonFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "places_example_1.json"); // places_example.json is required to be in the same directory as the test assembly
        var jsonContent = File.ReadAllText(jsonFilePath);
        var jsonResponse = JObject.Parse(jsonContent);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://maps.googleapis.com/maps/api/geocode/json*")
            .Respond("application/json", jsonContent); 
        mockHttp.When("https://maps.googleapis.com/maps/api/place/nearbysearch/json*")
            .Respond(HttpStatusCode.Unauthorized); // Simulate an invalid API key by returning 401 Unauthorized

        var _placesPlugin = new PlacesPlugin();
        _placesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _placesPlugin.ExecuteAsync(parameter);
        if(result.Contains("The request for the restaurants was denied. Check the credentials in the config.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }

    [TestMethod]
    public async Task NearbySearchNetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "Lerchenstraße 1, 70174 Stuttgart";

        var jsonFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "places_example_1.json"); // places_example.json is required to be in the same directory as the test assembly
        var jsonContent = File.ReadAllText(jsonFilePath);
        var jsonResponse = JObject.Parse(jsonContent);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://maps.googleapis.com/maps/api/geocode/json*")
            .Respond("application/json", jsonContent); 
        // Simulate a Networkerror for the nearby search by only responding to the geocode request

        var _placesPlugin = new PlacesPlugin();
        _placesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _placesPlugin.ExecuteAsync(parameter);
        if(result.Contains("An error has occurred while retrieving the restaurant information.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }

    [TestMethod]
    public async Task GeoCodeInvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var parameter = "";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://maps.googleapis.com/maps/api/geocode/json*")
                            .Respond(HttpStatusCode.Unauthorized); // Simulate an invalid API key by returning 401 Unauthorized

        var _placesPlugin = new PlacesPlugin();
        _placesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _placesPlugin.ExecuteAsync(parameter);
        if(result.Contains("The shares information could not be retrieved.")){
            exceptionThrown = true;
        }
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }

    [TestMethod]
    public async Task GeoCodeNetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "";
        
        // Simulate a network error by creating a mock HTTP client that does not respond
        var mockHttp = new MockHttpMessageHandler();

        var _placesPlugin = new PlacesPlugin();
        _placesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _placesPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        if (result.Contains("An error has occurred while retrieving the restaurant information.")){
            exceptionThrown = true;
        }
        
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for network error");
    }
}

