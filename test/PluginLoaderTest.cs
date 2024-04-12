namespace test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using WeatherPlugin;

[TestClass]
public class PluginLoaderTest
{
    // [TestMethod]
    //     public void Test_LoadPluginFromFile_Success()
    //     {
    //         // Arrange
    //         var pluginFolderPath = Path.Combine(Path.GetTempPath(), "TestPlugins");
    //         Directory.CreateDirectory(pluginFolderPath);
    //         string testPluginPath = Path.Combine(pluginFolderPath, "TestPlugin.dll");
    //         // Simulate a plugin dll
    //         File.WriteAllBytes(testPluginPath, new byte[] { 0x01, 0x00, 0x00, 0x00 }); // Dummy content

    //         PluginLoader pluginLoader = new PluginLoader(pluginFolderPath, "config.json");

    //         // Act
    //         pluginLoader.LoadPlugins(pluginFolderPath);

    //         // Assert
    //         Assert.IsTrue(pluginLoader._plugins.Count > 0);
    //         Assert.IsInstanceOfType(pluginLoader._plugins[0], typeof(IPlugin));

    //         // Cleanup
    //         Directory.Delete(pluginFolderPath, true);
    //     }
}