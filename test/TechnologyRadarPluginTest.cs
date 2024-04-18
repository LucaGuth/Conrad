namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using TechnologyRadarPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;



[TestClass]
public class TechnologyRadarPluginTest
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
        var parameter = "";
        var expectedString = "Technology trend in the category";

        var htmlFilePath = Path.Combine(@"..\..\..", "valid_api_responses/technology_radar_example.html");
        var htmlContent = File.ReadAllText(htmlFilePath);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.thoughtworks.com/de-de/radar/*")
            .Respond("text/html", htmlContent);

        var _technologyRadarPlugin = new TechnologyRadarPlugin();
        _technologyRadarPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _technologyRadarPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);

        // Assert
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "";

        var mockHttp = new MockHttpMessageHandler(); // No response configured to simulate network error
        var _technologyRadarPlugin = new TechnologyRadarPlugin();
        _technologyRadarPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        
        // Act
        var result = await _technologyRadarPlugin.ExecuteAsync(parameter);

        // Assert
        Assert.IsTrue(result.Contains("An error has occurred while processing the Technology Trend information"));
    }

    [TestMethod]
    public async Task BadRequestShouldBeHandled()
    {
        // Arrange
        var parameter = "";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.thoughtworks.com/de-de/radar/*")
            .Respond(System.Net.HttpStatusCode.BadRequest);

        var _technologyRadarPlugin = new TechnologyRadarPlugin();
        _technologyRadarPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _technologyRadarPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);

        // Assert
        Assert.IsTrue(result.Contains("The information for the technology trend could not be retrieved. Please check your internet connection and the service availability with the configured URL."));
    }

    [TestMethod]
    public async Task EmptyResponseShouldBeHandled()
    {
        // Arrange
        var parameter = "";
        
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.thoughtworks.com/de-de/radar/*")
            .Respond("text/html", "");

        var _technologyRadarPlugin = new TechnologyRadarPlugin();

        // Act
        _technologyRadarPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        var result = await _technologyRadarPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("An error has occurred while processing the Technology Trend information."));
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _technologyRadarPlugin = new TechnologyRadarPlugin();
        
        // Act
        var result = _technologyRadarPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("BaseUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _technologyRadarPlugin = new TechnologyRadarPlugin();
        var config = new TechnologyRadarPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _technologyRadarPlugin.LoadConfiguration(jsonNode);

        var result = _technologyRadarPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("test"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _technologyRadarPlugin = new TechnologyRadarPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _technologyRadarPlugin.LoadConfiguration(null);

        Console.WriteLine(_technologyRadarPlugin.GetConfigiguration());
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
internal class TechnologyRadarPluginTestConfig
{
    public string BaseUrl { get; set; } = "https://www.thoughtworks.com/de-de/radar/";

    public string[] TechnologyTrendUrlExtensions { get; set; } =
    [
        "test/",
        "tools/",
        "platforms/",
        "languages-and-frameworks/"
    ];
}