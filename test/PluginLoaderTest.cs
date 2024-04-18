namespace test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Reflection;
using PluginInterfaces;
using Sequencer;


[TestClass]
public class PluginLoaderTest
{
    // test init
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
    public void GetPluginsShouldReturnPlugins()
    {
        // Arrange
        var pluginLoader = new PluginLoader(@"TestPluginFolder", "PluginLoader_config.json"); // requires TestPluginFolder with example plugin dll 
        var mockPlugin = new Mock<IPlugin>();

        // Act
        var plugins = pluginLoader.GetPlugins<IPlugin>();

        // Assert
        Console.WriteLine(plugins);
        Console.WriteLine(plugins.Count());
        Assert.AreEqual(3, plugins.Count());    
    }

    [TestMethod]
    public void GetPluginsByNameShouldReturnPlugins()
    {
        // Arrange
        var pluginLoader = new PluginLoader(@"TestPluginFolder", "PluginLoader_config.json");
        var mockPlugin = new Mock<IPlugin>();
        var name = "ExampleNotifier";

        // Act
        var plugins = pluginLoader.GetPluginsByName<IPlugin>(name);

        // Assert
        Assert.AreEqual(1, plugins.Count());    
    }

    [TestMethod]
    public void NewPluginFolderShouldBeCreated()
    {
        // Arrange
        var pluginFolderPath = @"TestPluginFolder12341234";
        // Act
        var pluginLoader = new PluginLoader(pluginFolderPath ,"PluginLoader_config.json");


        // Assert
        Assert.IsTrue(Directory.Exists(pluginFolderPath));

        // cleanup 
        Directory.Delete(pluginFolderPath);
    }

    [TestMethod]
    public void ConfigurationShouldBeCreated()
    {
        // Arrange
        var pluginFolderPath = @"TestPluginFolder";
        var pluginLoader = new PluginLoader(pluginFolderPath, "new_PluginLoader_config.json");

        // Act
        Console.WriteLine(File.ReadAllText("new_PluginLoader_config.json"));

        // Assert
        Assert.IsTrue(File.Exists("new_PluginLoader_config.json"));
        

        // cleanup
        File.Delete("new_PluginLoader_config.json");
    }
}
