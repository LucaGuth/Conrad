namespace test
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PluginInterfaces;
    using Serilog;
    using WikipediaPlugin; 
    using RichardSzalay.MockHttp;
    using System.Net.Http.Headers;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    [TestClass]
    public class WikipediaPluginTest 
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
            var _wikipediaPlugin = new WikipediaPlugin(); 

            // Act
            var result = _wikipediaPlugin.GetConfigiguration().ToString();
            Console.WriteLine(result);
            // Assert
            Assert.IsTrue(result.Contains("BaseUrl"));
        }

        [TestMethod]
        public async Task LoadConfigurationShouldLoadConfiguration()
        {
            // Arrange
            var _wikipediaPlugin = new WikipediaPlugin(); 
            var config = new WikipediaPluginTestConfig(); 
            // configString to json
            var jsonNode = JsonNode.Parse(JsonSerializer.Serialize(config));

            // Act
            _wikipediaPlugin.LoadConfiguration(jsonNode);

            var result = _wikipediaPlugin.GetConfigiguration().ToString();
            // Assert
            Assert.IsTrue(result.Contains("TestUrl"));
        }

        [TestMethod]
        public async Task LoadConfigurationShouldThrowException()
        {
            // Arrange
            var _wikipediaPlugin = new WikipediaPlugin(); 

            // Act
            var exceptionThrown = false;
            try
            {
            _wikipediaPlugin.LoadConfiguration(null);

            Console.WriteLine(_wikipediaPlugin.GetConfigiguration());
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
    internal class WikipediaPluginTestConfig
    {
        public string BaseUrl { get; set; } = "TestUrl";
        public string DefaultKeyword { get; set; } = "Observer pattern";
    }
}




