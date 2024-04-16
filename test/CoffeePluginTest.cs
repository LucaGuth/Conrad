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



[TestClass]
public class CoffeePluginTest
{
    [TestMethod]
    public async Task ValidResponseShouldBeParsed()
    {
        // serilog console log
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        // Arrange
        var parameter = "ListOfFilters:'warm, milk frother, fruity'";
        var expectedString = "";

        var _coffeePlugin = new CoffeePlugin();
        var mockHttp = new MockHttpMessageHandler();
        
        var htmlContentPath1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "coffee_example_1.html");
        var htmlContent1 = File.ReadAllText(htmlContentPath1);
        var htmlContentPath2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "coffee_example_2.html");
        var htmlContent2 = File.ReadAllText(htmlContentPath2);

        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes?attributes=7E950F3C35AF4B84879284ED22719B58%2c70119DBFED7847C8A5819ED8FE23DE32%2cD0931460F83049F197ECB9A9CDEF5B33%2cBF989C52F9A74FB58A4140C426F00551")
                .Respond("text/html", htmlContent1);
        mockHttp.When("https://us.jura.com/en/about-coffee/coffee-recipes/milky-strawberry-dream*")
                .Respond("text/html", htmlContent2);

        _coffeePlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        var result = await _coffeePlugin.ExecuteAsync(parameter);

        Console.WriteLine(result);

        // Assert
        Assert.IsTrue(result.Contains(expectedString));
        Assert.IsTrue(false);

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
        Console.WriteLine(result);
        Assert.IsTrue(result.Contains("No coffee recipe found."));
        Assert.IsTrue(false);
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

        Console.WriteLine(result);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("An error has occurred while retrieving the coffee recipe."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(false);
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
}