namespace test;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PluginInterfaces;
using Serilog;
using UserInputNotifierPackage;
using System.Text.Json;
using System.Text.Json.Nodes;

[TestClass]
public class UserInputPluginTest
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
        var _userInputNotifier = new UserInputNotifier();
        
        // Act
        var result = _userInputNotifier.GetConfigiguration().ToString();
        Console.WriteLine(result);
        // Assert
        Assert.IsTrue(result.Contains("ListenAddress"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldLoadConfiguration()
    {
        // Arrange
        var _userInputNotifier = new UserInputNotifier();
        var config = new TestConfig();
        // configString to json
        var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

        // Act
        _userInputNotifier.LoadConfiguration(jsonNode);

        var result = _userInputNotifier.GetConfigiguration().ToString();
        // Assert
        Assert.IsTrue(result.Contains("testAddress"));
    }

    [TestMethod]
    public async Task LoadConfigurationShouldThrowException()
    {
        // Arrange
        var _userInputNotifier = new UserInputNotifier();

        // Act
        var exceptionThrown = false;
        try{
        _userInputNotifier.LoadConfiguration(null);

        Console.WriteLine(_userInputNotifier.GetConfigiguration());
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
    internal class TestConfig
    {
        public string ListenAddress { get; set; } = "testAddress";
        public int TcpPort { get; set; } = 4000;
        public int TcpReadTimeoutInMs { get; set; } = 1000;
    }