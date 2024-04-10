using PluginInterfaces;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Sequencer
{
    /// <summary>
    /// The main class of the sequence that is responsible for the execution of the plugins.
    /// </summary>
    internal class Sequence
    {

        /// <summary>
        /// The constructor of the sequence that initializes the plugins.
        /// </summary>
        /// <param name="pluginLoader"></param>
        public Sequence(PluginLoader pluginLoader)
        {
            Log.Information("Initializing Sequence");
            _pluginLoader = pluginLoader;

            Log.Information("Loading Notifier Plugins");
            _notifierPlugins = _pluginLoader.GetPlugins<INotifierPlugin>();

            foreach (var notifier in _notifierPlugins)
            {
                Log.Debug("Subscribing to notifications to {PluginName}", notifier.Name);
                notifier.OnNotify += OnNotify;
            }

            void OnConfigurationChange(IConfigurablePlugin plugin)
            {
                _pluginLoader.UpdateConfiguration();
            }

            Log.Information("Loading Configurable Plugins");
            var configurablePluigns = _pluginLoader.GetPlugins<IConfigurablePlugin>();
            foreach (var configurablePlugin in configurablePluigns)
            {
                Log.Debug("Subscribing to Configuration Change Event for Plugin {PluginName}", configurablePlugin.Name);
                configurablePlugin.OnConfigurationChange += OnConfigurationChange;
            }

            Log.Information("Initializing Plugins");

            object lockNotInitializedPlugins = new object();
            List<IPlugin> notInitializedPlugins = new List<IPlugin>();

            Parallel.ForEach(_pluginLoader.GetPlugins<IPlugin>(), plugin =>
            {
                Log.Debug("Initializing Plugin {PluginName}", plugin.Name);

                try
                {
                    plugin.Initialize();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error while initializing plugin {PluginName}. It will be removed from Plugin list.", plugin.Name);
                    lock (lockNotInitializedPlugins)
                    {
                        notInitializedPlugins.Add(plugin);
                    }
                }
            });

            foreach (var plugin in notInitializedPlugins)
            {
                _pluginLoader.RemovePlugin(plugin);
            }

            _outputPlugins = _pluginLoader.GetPlugins<IOutputPlugin>();
            _llm = _pluginLoader.GetPlugins<ILangaugeModel>().First();
        }

        /// <summary>
        /// Callback that runs whenever an INotifierPlugin has a notification.
        /// This is the main sequence of Conrad:
        /// notification -> llm -> IExecutorPlugin's -> llm -> output to user
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnNotify(INotifierPlugin sender, string message)
        {
            Log.Debug("[Sequencer] Received message from {PluginName}: {Message}", sender.Name, message);

            // llm
            var promt = GenerateInputPromt(sender, message);
            Log.Debug("Sending promt to LLM: {promt}", promt);
            var llmInputResponse = _llm.Process(promt);
            Log.Information("[Sequencer] [Parsing Stage] LLM response: {response}", llmInputResponse);

            // Parse response
            var responseCommands = llmInputResponse.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            StringBuilder executorResults = new();
            Parallel.ForEach(responseCommands, command =>
            {
                var parsedCommand = Regex.Match(command, @"([^:]+):?(.*)");
                if (parsedCommand.Success)
                {
                    var requestedPluginName = parsedCommand.Groups[1].Value.Trim();
                    var requestedPluginArguments = parsedCommand.Groups[2].Value.Trim();
                    try
                    {
                        var plugin = _pluginLoader.GetPluginsByName<IExecutorPlugin>(requestedPluginName).First();
                        var executorResult = plugin.ExecuteAsync(requestedPluginArguments).Result;
                        Log.Debug("Plugin {plugin} responded: {executorResult}", plugin.Name, executorResult);
                        if (executorResult != null)
                        {
                            lock (executorResults)
                            {
                                executorResults.AppendLine($"{plugin.Name}: {requestedPluginArguments}");
                                executorResults.AppendLine("```");
                                executorResults.AppendLine(executorResult);
                                executorResults.AppendLine("```");
                            }
                        }
                    }
                    catch (Exception) { }
                }
            });

            var executorResultsString = executorResults.ToString();

            Log.Debug("[Sequencer] Executor Results: {results}", executorResultsString);

            // llm
            var llmOutputResponse = _llm.Process(GenerateOutputPromt(sender, message, executorResultsString == string.Empty ? "No plugin could give response" : executorResultsString));
            Log.Debug("[Sequencer] [Parsing Stage] LLM response: {response}", llmOutputResponse);


            // output to user
            CancellationTokenSource outputCancelationToken = new CancellationTokenSource();
            List<Task> outputTasks = new List<Task>();
            object lockOutputTasks = new object();
            foreach (var outputPlugin in _outputPlugins)
            {
                var task = new Task(() => outputPlugin.PushMessage(llmOutputResponse), outputCancelationToken.Token);
                Log.Debug("Created task for {PluginName} with task ID {taskID}.", outputPlugin.Name, task.Id);
                task.ContinueWith(t =>
                {
                    lock (lockOutputTasks)
                    {
                        outputTasks.Remove(t);
                    }
                    Log.Debug("Task {taskID} completed.", t.Id);
                });
                outputTasks.Add(task);
                task.Start();
            }

            var time = Stopwatch.StartNew();
            while (outputTasks.Count > 0)
            {
                if (time.ElapsedMilliseconds > 60000)
                {
                    Log.Warning("Output Plugins are taking too long to respond. Cancelling tasks: {tasks}", outputTasks);
                    outputCancelationToken.Cancel();
                    break;
                }
            }
        }

        /// <summary>
        /// The main method of the sequence that starts the plugins.
        /// </summary>
        public void Run()
        {
            Log.Information("Preparing notifier services.");
            List<Task> tasks = [];
            foreach (var notifier in _notifierPlugins)
            {
                var task = new Task(notifier.Run);
                Log.Debug("Created task for {PluginName} with task ID {taskID}.", notifier.Name, task.Id);
                tasks.Add(task);
            }

            foreach (var task in tasks)
            {
                Log.Debug("Starting task {task}.", task.Id);
                task.Start();
            }

            // Idle loop
            Log.Information("Sequence Running...");
            Thread.Sleep(Timeout.Infinite);
        }

        private string GenerateInputPromt(INotifierPlugin notifierPlugin, string message)
        {
            const string _llmPromtHeader = @"
You are a personal digital assistant. You have access to the following plugins:
";
            StringBuilder promt = new StringBuilder(_llmPromtHeader);
            foreach (var plugin in _pluginLoader.GetPlugins<IExecutorPlugin>())
            {
                promt.AppendLine($"{plugin.Name}: {plugin.ParameterFormat}");
            }

            const string _llmPromtBody = @"

- Remove plugins that are not relevant to the task.
- Fill out all parameters sensibly (everything inside {}).
  If not all parameters can be filled out, remove that plugin.
  Plugins may have no parameters, in that case simply return the plugin name, as shown above.
- If no plugin is relevant, return '-'.
- You may only return the same plugin multiple times if each instance has different parameters.

Do not, under any circumstances, in any way explain the result you give!
The output will be parsed, so it has to adhere exactly to the format shown above and cannot contain anything extra!
The user will not see the response you give, you are talking to a machine that only needs to know which plugins to execute!

";
            promt.AppendLine(_llmPromtBody);

            promt.AppendLine($"Input was received from '{notifierPlugin.Name} ({notifierPlugin.Description})'");
            promt.AppendLine($"```");
            promt.AppendLine(message);
            promt.AppendLine($"```");

            return promt.ToString();
        }

        private string GenerateOutputPromt(INotifierPlugin sender, string request, string results)
        {
            StringBuilder promt = new($"You are a personal digital assistant. The Plugin {sender.Name}({sender.Description}) made the \"{request}\". The request was processed by the application.");

            promt.Append("```");
            promt.Append(results);
            promt.Append("```");

            promt.Append("Your name is Conrad. Summarize the results for the User in a friendly way.");

            return promt.ToString();

        }

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;

        private readonly IEnumerable<IOutputPlugin> _outputPlugins;

        private readonly ILangaugeModel _llm;
    }
}
