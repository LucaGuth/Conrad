namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using PreferencesPlugin;
using System.Text.Json;
using System.Text.Json.Nodes;


[TestClass]
public class PreferencesPluginTest
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
    public async Task GetConfigurationShouldReturnConfiguration()
    {
        // Arrange
        var _preferencesPlugin = new PreferencesPlugin();

        // Act
        var result = _preferencesPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("Preferences"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _preferencesPlugin = new PreferencesPlugin();
        var config = new PreferencesPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _preferencesPlugin.LoadConfiguration(jsonNode);

        var result = _preferencesPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("test"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _preferencesPlugin = new PreferencesPlugin();

        // Act
        var exceptionThrown = false;
        try
        {
            _preferencesPlugin.LoadConfiguration(null);

            Console.WriteLine(_preferencesPlugin.GetConfigiguration());
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
internal class PreferencesPluginTestConfig
{
    public Dictionary<string, string> Preferences { get; set; } = new Dictionary<string, string>() { { "test", "test" } };
}