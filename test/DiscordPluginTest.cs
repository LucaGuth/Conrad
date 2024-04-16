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

[TestClass]
public class DiscordPluginTest
{
    // [TestMethod]
    // public async Task ValidResponseShouldBeParsed()
    // {
    //     // Arrange
    //     var mockHttp = new MockHttpMessageHandler();
    //     mockHttp.Expect("*")
    //         .WithPartialContent("Hello");

    //     mockHttp.Expect("*")
    //         .With(request => request.Headers.Accept.Contains(new MediaTypeHeaderValue("audio/ogg")));


    //     var discordOutputPlugin = new DiscordOutputPlugin();
    //     var message = "Hello";
    //     // Act
    //     discordOutputPlugin.Initialize();
    //     discordOutputPlugin.PushMessage(message);
    //     // Assert
    //     mockHttp.VerifyNoOutstandingExpectation();
    // }

}