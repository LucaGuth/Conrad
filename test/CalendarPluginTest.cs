namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using CalendarPlugin;
using Serilog; 
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;


[TestClass]
public class CalendarPluginTest
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

        // Act

        // Assert
    }

    [TestMethod]
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _calendarPlugin = new CalendarPlugin();
        
        // Act
        var result = _calendarPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("ServiceAccount"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _calendarPlugin = new CalendarPlugin();
        var config = new CalendarPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _calendarPlugin.LoadConfiguration(jsonNode);

        var result = _calendarPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("TestName"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _calendarPlugin = new CalendarPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _calendarPlugin.LoadConfiguration(null);
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
internal class CalendarPluginTestConfig
{
    public ServiceAccount ServiceAccount { get; set; } = new();
    public string CalendarId { get; set; } = "";
    public string ApplicationName { get; set; } = "TestName";
}