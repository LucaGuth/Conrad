namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NewsPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

[TestClass]
public class NewsPluginTest
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
        var parameter = ""; // no parameter needed
        var expectedString = @"News:";

        var newsExampleFilePath = Path.Combine(@"..\..\..", "valid_api_responses/news_example.xml"); 
        var newsExample = File.ReadAllText(newsExampleFilePath);
        var newsXml = XDocument.Parse(newsExample);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.tagesschau.de/xml/rss2")
            .Respond("application/xml", newsExample);
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());        
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        var numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.AreEqual(4, numLines);
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task InvalidResponseShouldNotBeParsed()
    {
        // Arrange
        var parameter = ""; // no parameter needed
        var invalidRespone = "invalid response";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.tagesschau.de/xml/rss2")
            .Respond("application/xml", invalidRespone);
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("The news data could not be parsed."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = ""; // no parameter needed
        var mockHttp = new MockHttpMessageHandler(); // no response setup
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        // Assert
        Console.WriteLine(result);
        var exceptionThrown = false;
        if (result.Contains("An error occurred while processing the news data."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _newsPlugin = new NewsPlugin();
        
        // Act
        var result = _newsPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("BaseUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _newsPlugin = new NewsPlugin();
        var config = new NewsPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _newsPlugin.LoadConfiguration(jsonNode);

        var result = _newsPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("TestUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _newsPlugin = new NewsPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _newsPlugin.LoadConfiguration(null);

        Console.WriteLine(_newsPlugin.GetConfigiguration());
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
internal class NewsPluginTestConfig
{
    public string BaseUrl { get; set; } = "TestUrl";
}
