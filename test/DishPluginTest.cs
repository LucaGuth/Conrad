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

[TestClass]
public class DishPluginTest
{
    // [TestMethod]
    // public async Task ValidResponseShouldBeParsed()
    // {
    //     // Arrange
    
    //     // Act

    //     // Assert
    // }

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
