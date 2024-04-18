namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using CoffeePlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;



[TestClass]
public class CoffeePluginTest
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
        var parameter = "ListOfFilters:'warm, milk frother, fruity'";
        var expectedString = "You can prepare the delicious coffee beverage 'Milky Strawberry Dream', which has the description 'Try a sweet and fruity start to spring', as follows: Pour the white chocolate sauce into a small latte macchiato glass. Add the milk and strawberry syrup to the Automatic Milk Frother and prepare a serving of warm milk foam. As soon as the Automatic Milk Frother stops, pour the warm milk foam into the glass until it is 3/4 full. Prepare the espresso in a separate receptacle. Pour it carefully into the glass and serve. For more information, please visit the Jura website";

        var _coffeePlugin = new CoffeePlugin();
        var mockHttp = new MockHttpMessageHandler();

        var htmlContentPath1 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "valid_api_responses/coffee_example_1.html");
        var htmlContent1 = File.ReadAllText(htmlContentPath1);
        var htmlContentPath2 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "valid_api_responses/coffee_example_2.html");
        var htmlContent2 = File.ReadAllText(htmlContentPath2);

        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes?attributes=7E950F3C35AF4B84879284ED22719B58%2c70119DBFED7847C8A5819ED8FE23DE32%2cD0931460F83049F197ECB9A9CDEF5B33%2cBF989C52F9A74FB58A4140C426F00551")
                .Respond("text/html", htmlContent1);
        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes/milky-strawberry-dream*")
                .Respond("text/html", htmlContent2);

        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);

        // Assert
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task EmptyResponseShouldNotBeParsed()
    {
        // Arrange
        var parameter = "ListOfFilters:''";
        var _coffeePlugin = new CoffeePlugin();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes*")
                .Respond("application/json", "");

        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);
        // Assert
        Assert.IsTrue(result.Contains("An error has occurred while retrieving the coffee recipe."));
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "ListOfFilters:'with milk, cold'";
        var _coffeePlugin = new CoffeePlugin();

        var mockHttp = new MockHttpMessageHandler();


        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);

        // Assert
        var exceptionThrown = false;
        if (result.Contains("An error has occurred while retrieving the coffee recipe."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NoResponseFoundShouldBeHandled()
    {
        // Arrange
        var parameter = "ListOfFilters:'with milk, cold'";
        var _coffeePlugin = new CoffeePlugin();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes*")
                .Respond(HttpStatusCode.NotFound);

        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);
        // Assert
        Assert.IsTrue(result.Contains("No coffee page found."));
    }

    [TestMethod]
    public async Task BadRequestShouldBeHandled()
    {
        // Arrange
        var parameter = "ListOfFilters:'with milk, cold'";
        var _coffeePlugin = new CoffeePlugin();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes*")
                .Respond(HttpStatusCode.BadRequest);

        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());

        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);
        // Assert
        Assert.IsTrue(result.Contains("The coffee recipe could not be retrieved."));
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _coffeePlugin = new CoffeePlugin();
        
        // Act
        var result = _coffeePlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("BaseUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _coffeePlugin = new CoffeePlugin();
        var config = new CoffeePluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _coffeePlugin.LoadConfiguration(jsonNode);

        var result = _coffeePlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("TestUrl"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _coffeePlugin = new CoffeePlugin();

        // Act
        var exceptionThrown = false;
        try{
        _coffeePlugin.LoadConfiguration(null);

        Console.WriteLine(_coffeePlugin.GetConfigiguration());
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
internal class CoffeePluginTestConfig
{
    public string BaseUrl { get; set; } = "TestUrl";
}
