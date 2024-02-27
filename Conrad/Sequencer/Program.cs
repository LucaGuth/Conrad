using Sequencer;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.CommandLine;
using System.Runtime.InteropServices.Marshalling;

namespace scl;

class Program
{
    private static readonly string Logo = @"
   _____                          _
  / ____|                        | |
 | |     ___  _ __  _ __ __ _  __| |
 | |    / _ \| '_ \| '__/ _` |/ _` |
 | |___| (_) | | | | | | (_| | (_| |
  \_____\___/|_| |_|_|  \__,_|\__,_|

Cognitive Optimizer for Notifications, Recommendations and Automated Data management

";
    static async Task<int> Main(string[] args)
    {
        // Logo
        Console.WriteLine(Logo);

        var conradBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".conrad");
        var conradConfigPath = Path.Combine(conradBasePath, "config.json");
        var conradPluginPath = Path.Combine(conradBasePath, "plugins");

        var logLevel = new Option<LogEventLevel>(
            name: "--log-level",
            description: "The log level to use for the program.");
        logLevel.AddAlias("-l");
        logLevel.SetDefaultValue(LogEventLevel.Information);

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

        var rootCommand = new RootCommand("Conrad - Cognitive Optimizer for Notifications, Recommendations and Automated Data management");

        rootCommand.AddOption(logLevel);
        rootCommand.AddOption(pluginFolder);
        rootCommand.AddOption(configFile);
        rootCommand.AddOption(generateConfig);

        rootCommand.SetHandler(RunProgram!, configFile, pluginFolder, logLevel, generateConfig);

        return await rootCommand.InvokeAsync(args);
    }

    internal static void RunProgram(string configFile, string pluginPath, LogEventLevel logEventLevel, bool generateConfig)
    {
        try
        {
            // Setup the logger
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logEventLevel)
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Literate)
                .WriteTo.File("log.txt")
                .CreateLogger();

            // Start the program
            Log.Information("Starting the program");
            PluginLoader pluginLoader = new(pluginPath, configFile);
            if (!generateConfig)
            {
                var sequence = new Sequence(pluginLoader);
                sequence.Run();
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Log.Fatal(ex, "An error occurred");
        }
        finally
        {
            Log.Information("Shutting down the program");
            Log.CloseAndFlush();
        }
    }
}