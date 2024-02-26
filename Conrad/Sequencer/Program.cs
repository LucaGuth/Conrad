using Sequencer;
using Serilog;
using System.CommandLine;

namespace scl;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup the logger 
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("log.txt")
            .CreateLogger();
        Log.Information("Starting the program");

            try{
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

            var generateConfig = new Option<bool>(
                name: "--generate-config",
                description: "Generate or update the current configuration file based on the current set of plugins. The Program will not start the sequencer and exit immediately after generating the configuration file.");
                configFile.SetDefaultValue(false);
            configFile.SetDefaultValue(false);

            var rootCommand = new RootCommand("Conrad - Cognitive Optimizer for Notifications, Recommendations and Automated Data management");

            rootCommand.AddOption(pluginFolder);
            rootCommand.AddOption(configFile);
            rootCommand.AddOption(generateConfig);

            rootCommand.SetHandler(RunProgram!, configFile, pluginFolder, generateConfig);

            return await rootCommand.InvokeAsync(args);
        }
        catch(Exception ex)
        {
            Log.Error(ex, "An error occurred");
            return 1;
        }
        finally
        {
            Log.Information("Shutting down the program");
            Log.CloseAndFlush();
        }
    }

    internal static void RunProgram(string configFile, string pluginPath, bool generateConfig)
    {
        PluginLoader pluginLoader = new(pluginPath, configFile);

        if (!generateConfig)
        {
            var sequence = new Sequence(pluginLoader);
            sequence.Run();
        }
    }
}