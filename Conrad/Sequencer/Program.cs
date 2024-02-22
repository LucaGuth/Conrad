using Sequencer;
using System.CommandLine;

namespace scl;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var conradBasePath   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".conrad");
        var conradConfigPath = Path.Combine(conradBasePath, "config.json");
        var conradPluginPath = Path.Combine(conradBasePath, "plugins");

        var pluginFolder = new Option<string?>(
            name: "--plugins",
            description: "The folder to load all plugins from.");
        pluginFolder.AddAlias("-p");
        pluginFolder.SetDefaultValue(conradPluginPath);

        var configFile = new Option<string?>(
            name: "--config",
            description: "The configuration for the plugins");
        configFile.AddAlias("-c");
        configFile.SetDefaultValue(conradConfigPath);

        var rootCommand = new RootCommand("Conrad - Cognitive Optimizer for Notifications, Recommendations and Automated Data management");

        rootCommand.AddOption(pluginFolder);
        rootCommand.AddOption(configFile);

        rootCommand.SetHandler(RunProgram!, configFile, pluginFolder);

        return await rootCommand.InvokeAsync(args);
    }

    internal static void RunProgram(string configFile, string pluginPath)
    {
        PluginLoader pluginLoader = new(pluginPath, configFile);

        var sequence = new Sequence(pluginLoader);
        sequence.Run();
    }
}