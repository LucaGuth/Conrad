namespace test; 

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SharesPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;



[TestClass]
public class SharesPluginTest
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
        var symbol = "AAPL";
        var expectedString = "On 2024-04-10, 22:00: Open Price: $167.7600, Close Price: $168.0250, Trading Volume: 12867809 shares";


        // Load valid JSON response from file
        var jsonFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "shares_example.json"); // shares_example.json is required to be in the same directory as the test assembly
        var jsonContent = File.ReadAllText(jsonFilePath);
        var jsonResponse = JObject.Parse(jsonContent);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.alphavantage.co/*")
                .Respond("application/json", jsonContent);
        var _sharesPlugin = new SharesPlugin();
        _sharesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _sharesPlugin.ExecuteAsync(symbol);

        // Assert
        int numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.IsTrue(numLines == 6, "The result should contain 4 lines");
        Assert.IsTrue(result.Contains(expectedString), "The result should contain the expected shares information");
    }   


    [TestMethod]
    public async Task InvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var symbol = "test_symbol";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://www.alphavantage.co/*")
                            .Respond(HttpStatusCode.Unauthorized); // Simulate an invalid API key by returning 401 Unauthorized

        var _sharesPlugin = new SharesPlugin();
        _sharesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;       
        var result = await _sharesPlugin.ExecuteAsync(symbol);
        if(result.Contains("The shares information could not be retrieved.")){
            exceptionThrown = true;
        }
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for invalid API key");
    }   

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var symbol = "test_symbol";
        
        // Simulate a network error by creating a mock HTTP client that throws an exception
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("unreachable_url"); // no response configured, will cause a network error

        var _sharesPlugin = new SharesPlugin();
        _sharesPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var exceptionThrown = false;
        var result = await _sharesPlugin.ExecuteAsync(symbol);
        if (result.Contains("An error occurred while retrieving the shares information.")){
            exceptionThrown = true;
        }
        
        // Assert
        Assert.IsTrue(exceptionThrown, "An exception should have been thrown for network error");
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _sharesPlugin = new SharesPlugin();

        // Act
        var result = _sharesPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("ApiKey"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _sharesPlugin = new SharesPlugin();
        var config = new SharesPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _sharesPlugin.LoadConfiguration(jsonNode);

        var result = _sharesPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("TestUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _sharesPlugin = new SharesPlugin();

        // Act
        var exceptionThrown = false;
        try
        {
            _sharesPlugin.LoadConfiguration(null);

            Console.WriteLine(_sharesPlugin.GetConfigiguration());
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
internal class SharesPluginTestConfig
{
    public string ApiKey { get; set; } = "";
    public string Interval { get; set; } = "60min";
    public string BaseUrl { get; set; } = "TestUrl";
}