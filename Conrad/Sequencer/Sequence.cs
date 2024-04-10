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
            var plugin_prompt = GenerateInputPrompt(sender, message);
            Log.Debug("[Sequencer] [Plugin Stage] Sending prompt to LLM: {prompt}", plugin_prompt);
            var llmInputResponse = _llm.Process(plugin_prompt);
            Log.Information("[Sequencer] [Plugin Stage] LLM response: {response}", llmInputResponse);

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
                        if (executorResult != null && executorResult != string.Empty)
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
            var summary_prompt = GenerateOutputPrompt(sender, message, executorResultsString == string.Empty ? "There are no plugin responses" : executorResultsString);
            Log.Debug("[Sequencer] [Summary Stage] Sending prompt to LLM: {prompt}", summary_prompt);
            var llmOutputResponse = _llm.Process(summary_prompt);
            Log.Debug("[Sequencer] [Summary Stage] LLM response: {response}", llmOutputResponse);


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

        private string GenerateInputPrompt(INotifierPlugin notifierPlugin, string message)
        {
            StringBuilder prompt = new StringBuilder("You are a personal digital assistant. You have access to the following plugins:\n");
            foreach (var plugin in _pluginLoader.GetPlugins<IExecutorPlugin>())
            {
                prompt.AppendLine($"{plugin.Name}: {plugin.ParameterFormat}");
            }

            const string _llmPromptBody = @"

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
            prompt.AppendLine(_llmPromptBody);

            prompt.AppendLine($"Input was received from '{notifierPlugin.Name} ({notifierPlugin.Description})'");
            prompt.AppendLine($"```");
            prompt.AppendLine(message);
            prompt.AppendLine($"```");

            return prompt.ToString();
        }

        private string GenerateOutputPrompt(INotifierPlugin sender, string request, string results)
        {
            StringBuilder prompt = new($"You are a personal digital assistant.");

            prompt.AppendLine("Some plugins were executed to give you background information for answering the request.");
            prompt.AppendLine("Here are the plugins with their arguments, followed by the results in backticks - please do not confuse them.");
            prompt.AppendLine(results);

            prompt.AppendLine("Write an answer to the request. Do not provide all information from the plugins, just because you have it - only answer sensibly.");
            prompt.AppendLine("Do not reiterate the data inside the plugin requests.");
            prompt.AppendLine("Keep your answer as short as possible! Like, very very short okay? SUPER SHORT!");
            prompt.AppendLine("Answer in full sentences, the output will be the input for a text to speech system.");

            prompt.AppendLine($"You received a request from {sender.Name} ({sender.Description}):\n```\"{request}\"```.\n");

            return prompt.ToString();

        }

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;

        private readonly IEnumerable<IOutputPlugin> _outputPlugins;

        private readonly ILangaugeModel _llm;
    }
}
