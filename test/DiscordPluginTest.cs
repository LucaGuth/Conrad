namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using DiscordOutputPlugin;
using RichardSzalay.MockHttp;
using System.Net.Http.Headers;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;


[TestClass]
public class DiscordPluginTest
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
        var _discordPlugin = new DiscordOutputPlugin();
        
        // Act
        var result = _discordPlugin.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("/python_venv/bin/piper"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _discordPlugin = new DiscordOutputPlugin();
        var config = new PiperOutputPluginTestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _discordPlugin.LoadConfiguration(jsonNode);

        var result = _discordPlugin.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("testPath"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _discordPlugin = new DiscordOutputPlugin();

        // Act
        var exceptionThrown = false;
        try{
        _discordPlugin.LoadConfiguration(null);

        Console.WriteLine(_discordPlugin.GetConfigiguration());
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
    public class PiperOutputPluginTestConfig
    {
        public string PiperPath { get; set; } = "testPath";
        public string ModelPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx";
        public string ConfigPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx.json";
        public string TempPath { get; set; } = "./tmp";
        public string DiscordBotEndpoint { get; set; } = "http://test";
        public double LengthScale { get; set; } = 1.0;
    }