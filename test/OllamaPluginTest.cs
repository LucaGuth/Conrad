namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using OllamaPluginPackage;
using System.Text.Json;
using System.Text.Json.Nodes;

[TestClass]
public class OllamaPluginTest
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
        var _ollamaPlugin = new OllamaPlugin();

        // Act
        var result = _ollamaPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);

        // Assert
        Assert.IsTrue(result.Contains("Uri"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _ollamaPlugin = new OllamaPlugin();
        var config = new OllamaTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _ollamaPlugin.LoadConfiguration(jsonNode);

        var result = _ollamaPlugin.GetConfigiguration().ToString();

        // Assert
        Assert.IsTrue(result.Contains("TestUri"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _ollamaPlugin = new OllamaPlugin();

        // Act
        var exceptionThrown = false;
        try
        {
            _ollamaPlugin.LoadConfiguration(null);
            Console.WriteLine(_ollamaPlugin.GetConfigiguration());
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
public class OllamaTestConfig
{
    public string Uri { get; set; } = "TestUri";
    public string Model { get; set; } = "gemma:latest";
    public int Timeout { get; set; } = 60000;
}