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
using QuizPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;


[TestClass]
public class QuizPluginTest
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
        var parameter = "";
        var expectedString = "Question: \"Windows NT\" is a monolithic kernel. Answer: False";
        var JsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "valid_api_responses/quiz_example.json");
        var JsonContent = File.ReadAllText(JsonFilePath);

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://opentdb.com/api.php*")
                            .Respond("application/json", JsonContent);

        var _quizPlugin = new QuizPlugin();
        _quizPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        
        // act
        var result = await _quizPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        
        // assert
        var numLines = result.Split('\n').Length;
        Assert.IsTrue(numLines == 4);
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task BadRequestShouldBeHandled()
    {
        // arrange
        var parameter = "";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://opentdb.com/api.php*")
                            .Respond(HttpStatusCode.BadRequest); // Simulate too many requests by returning 429 Too Many Requests
        var _quizPlugin = new QuizPlugin();
        _quizPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // act
        var exceptionThrown = false;
        var result = await _quizPlugin.ExecuteAsync(parameter);
        if (result.Contains("The quiz information could not be retrieved."))
        {
            exceptionThrown = true;
        }
        Console.WriteLine(result);

        // assert
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task TooManyRequestsShouldBeHandled()
    {
        // arrange
        var parameter = "";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://opentdb.com/api.php*")
                            .Respond(HttpStatusCode.TooManyRequests); // Simulate too many requests by returning 429 Too Many Requests
        var _quizPlugin = new QuizPlugin();
        _quizPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // act
        var exceptionThrown = false;
        var result = await _quizPlugin.ExecuteAsync(parameter);
        if (result.Contains("This plugin can only be used once every 5 seconds. Please try again later."))
        {
            exceptionThrown = true;
        }
        Console.WriteLine(result);

        // assert
        Assert.IsTrue(exceptionThrown, "The plugin should handle too many requests gracefully.");
    }

    [TestMethod]
    public async Task InvalidJsonShouldBeHandled()
    {
        // arrange
        var parameter = "";
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://opentdb.com/api.php*")
                            .Respond("application/json", "This is not a valid JSON response.");
        var _quizPlugin = new QuizPlugin();
        _quizPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // act
        var exceptionThrown = false;
        var result = await _quizPlugin.ExecuteAsync(parameter);
        if (result.Contains("An error has occurred while retrieving the quiz information."))
        {
            exceptionThrown = true;
        }
        Console.WriteLine(result);

        // assert
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled(){
        // arrange
        var parameter = "";
        var mockHttp = new MockHttpMessageHandler(); // no response set up
        var _quizPlugin = new QuizPlugin();
        _quizPlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // act
        var exceptionThrown = false;
        var result = await _quizPlugin.ExecuteAsync(parameter);
        if (result.Contains("An error has occurred while retrieving the quiz information."))
        {
            exceptionThrown = true;
        }
        Console.WriteLine(result);

        // assert
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _quizPlugin = new QuizPlugin();
        
        // Act
        var result = _quizPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("BaseUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _quizPlugin = new QuizPlugin();
        var config = new QuizPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _quizPlugin.LoadConfiguration(jsonNode);

        var result = _quizPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("testUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _quizPlugin = new QuizPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _quizPlugin.LoadConfiguration(null);

        Console.WriteLine(_quizPlugin.GetConfigiguration());
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
internal class QuizPluginTestConfig
{
    public string BaseUrl { get; set; } = "testUrl";
}
