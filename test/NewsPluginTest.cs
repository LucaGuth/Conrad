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
using NewsPlugin;
using RichardSzalay.MockHttp;
using System.Net;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

[TestClass]
public class NewsPluginTest
{
    [TestMethod]
    public async Task ValidResponseShouldBeParsed()
    {
        // Arrange
        var parameter = ""; // no parameter needed
        var expectedString = @"News:";

        var newsExampleFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "news_example.xml");
        var newsExample = File.ReadAllText(newsExampleFilePath);
        var newsXml = XDocument.Parse(newsExample);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.tagesschau.de/xml/rss2")
            .Respond("application/xml", newsExample);
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());        
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        Console.WriteLine(result);
        // Assert
        var numLines = result.Count(c => c.Equals('\n')) + 1;
        Assert.AreEqual(4, numLines);
        Assert.IsTrue(result.Contains(expectedString));
    }

    [TestMethod]
    public async Task InvalidResponseShouldNotBeParsed()
    {
        // Arrange
        var parameter = ""; // no parameter needed
        var invalidRespone = "invalid response";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://www.tagesschau.de/xml/rss2")
            .Respond("application/xml", invalidRespone);
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        // Assert
        var exceptionThrown = false;
        if (result.Contains("The news data could not be parsed."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public async Task NetworkErrorShouldBeHandled()
    {
        // Arrange
        var parameter = ""; // no parameter needed
        var mockHttp = new MockHttpMessageHandler(); // no response setup
        var _newsPlugin = new NewsPlugin();
        _newsPlugin.InjectHttpClient(mockHttp.ToHttpClient());
        // Act
        string result = await _newsPlugin.ExecuteAsync(parameter);
        // Assert
        Console.WriteLine(result);
        var exceptionThrown = false;
        if (result.Contains("An error occurred while processing the news data."))
        {
            exceptionThrown = true;
        }
        Assert.IsTrue(exceptionThrown);
    }
}
