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
using FoodPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;


[TestClass]
public class DishPluginTest
{
    [TestMethod]
    public async Task ValidResponseShouldBeParsed()
    {
        // add serilog console log
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();


        // Arrange
        var parameter = "dish:'pasta', cuisine:'italian'";
        var expectedString = "For the dish 'BLT Pizza' one needs the following ingredients: shredded colby jack cheese, fat free light cream cheese, garlic powder, lettuce, pizza crust, light ranch dressing, diced tomato, cooked turkey bacon"; 

        var jsonFilePath1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dish_example_1.json");
        var jsonContent1 = File.ReadAllText(jsonFilePath1);
        var jsonResponse1 = JObject.Parse(jsonContent1);
        var jsonFilePath2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dish_example_2.json");
        var jsonContent2 = File.ReadAllText(jsonFilePath2);
        var jsonResponse2 = JObject.Parse(jsonContent2);
    
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.spoonacular.com/recipes/complexSearch*")
            .Respond("application/json", jsonContent1);
        mockHttp.When("https://api.spoonacular.com/recipes/680975/*")
            .Respond("application/json", jsonContent2);

        var _dishPlugin = new DishPlugin();
        _dishPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        
        // Act
        string result = await _dishPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task EmptyResponseShouldNotBeParsed()
    {
        // Arrange
        var parameter = "dish:'pasta', cuisine:'italian'";
        var emptyJson = "{}";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.spoonacular.com/recipes/*")
            .Respond("application/json", emptyJson);
        var _dishPlugin = new DishPlugin();
        _dishPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _dishPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("An error has occurred while retrieving the dish information."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = "dish:'pasta', cuisine:'italian'";
        var mockHttp = new MockHttpMessageHandler(); // no response setup
        var _dishPlugin = new DishPlugin();
        _dishPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _dishPlugin.ExecuteAsync(parameter);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("An error has occurred while retrieving the dish information."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task InvalidApiKeyShouldBeHandled()
    {
        // Arrange
        var parameter = "dish:'pasta', cuisine:'italian'";
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.spoonacular.com/recipes/*")
            .Respond(HttpStatusCode.Unauthorized);
        var _dishPlugin = new DishPlugin();
        _dishPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _dishPlugin.ExecuteAsync(parameter);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("The API key is invalid."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NotFoundShouldBeHandled()
    {
        // Arrange
        var parameter = "dish:'pasta', cuisine:'italian'";
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.spoonacular.com/recipes/*")
            .Respond(HttpStatusCode.NotFound);
        var _dishPlugin = new DishPlugin();
        _dishPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _dishPlugin.ExecuteAsync(parameter);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("No recipe found for the given dish."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }
}
