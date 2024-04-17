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

            var promptHistory = GeneratePromptHistory();

            // llm
            var plugin_prompt = GeneratePluginPrompt(promptHistory, sender, message);
            Log.Debug("[Sequencer] [Plugin Stage] Sending prompt to LLM: {prompt}", plugin_prompt);
            var llmInputResponse = _llm.Process(plugin_prompt);
            Log.Information("[Sequencer] [Plugin Stage] LLM response: {response}", llmInputResponse);

            // add message prompt history
            if (_promptHistory.Capacity > 0) {
                if (_promptHistory.Count == _promptHistory.Capacity) {
                    _promptHistory.RemoveAt(0);
                }
                _promptHistory.Add((DateTime.Now, $"{sender.Name} ({sender.Description}): {message}"));
            }

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
                        var plugin = _pluginLoader.GetPlugins<IExecutorPlugin>().Where(plugin => requestedPluginName.Contains(plugin.Name)).First();
                        var executorResult = plugin.ExecuteAsync(requestedPluginArguments).Result;
                        Log.Debug("Plugin {plugin} responded: {executorResult}", plugin.Name, executorResult);
                        if (executorResult != null && executorResult != string.Empty)
                        {
                            lock (executorResults)
                            {
                                executorResults.AppendLine($"Plugin '{plugin.Name}' was called with parameters: {requestedPluginArguments}");
                                executorResults.AppendLine("```");
                                executorResults.AppendLine(executorResult);
                                executorResults.AppendLine("```");
                                executorResults.AppendLine();
                            }
                        }
                    }
                    catch (Exception) { }
                }
            });

            var executorResultsString = executorResults.ToString();

            Log.Debug("[Sequencer] Executor Results: {results}", executorResultsString);

            // llm
            var summary_prompt = GenerateOutputPrompt(promptHistory, sender, message, executorResultsString == string.Empty ? "There are no plugin responses" : executorResultsString);
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
                if (time.ElapsedMilliseconds > long.MaxValue)
                {
                    Log.Warning("Output Plugins are taking too long to respond. Cancelling tasks: {tasks}", outputTasks);
                    outputCancelationToken.Cancel();
                    break;
                }
            }

            if (_promptHistory.Capacity > 0) {

                if (_promptHistory.Count == _promptHistory.Capacity) {
                    _promptHistory.RemoveAt(0);
                }
                _promptHistory.Add((DateTime.Now, $"your response: {llmOutputResponse}"));
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

        private string GeneratePromptHistory() {
            StringBuilder history = new StringBuilder();
            DateTime currentTime = DateTime.Now;
            foreach (var entry in _promptHistory) {
                var timeSince = currentTime - entry.Item1;
                history.AppendLine($"[{timeSince.ToString()} ago] {entry.Item2}".Trim('\n'));
            }
            return history.ToString();
        }

        private string GeneratePluginPrompt(string promptHistory, INotifierPlugin notifierPlugin, string message)
        {
            StringBuilder prompt = new StringBuilder("You are a personal digital assistant called Conrad.\n");


            prompt.AppendLine();
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine("Here is the message history:");
            prompt.AppendLine(promptHistory);
            prompt.AppendLine("--------------------------------------------------------");


            prompt.AppendLine("To fulfill the request the following background information might be helpful:");
            foreach (var plugin in _pluginLoader.GetPlugins<IPromptAdderPlugin>())
            {
                prompt.AppendLine($" - {plugin.Name}:");
                prompt.AppendLine($"\t{plugin.PromptAddOn.Trim()}");
            }
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine();

            prompt.AppendLine("You have access to plugins that you can use to fulfill specific tasks. Plugins can have parameters that are needed to fulfill the request. You should only return the plugins with names and their parameters if they are necessary. The result will be machine parsed and is not allowed to have an explaination.");
            prompt.AppendLine("Here is a list of plugins. Your job is to call all relevant plugins.");
            prompt.AppendLine();

            foreach (var plugin in _pluginLoader.GetPlugins<IExecutorPlugin>())
            {
                prompt.AppendLine($"{plugin.Name}: {plugin.ParameterFormat}");
                prompt.AppendLine($"\tDescription: {plugin.Description}");
                prompt.AppendLine();
            }


            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine();

            prompt.AppendLine($"Your task is to call all plugins that are relevant to the following request received from '{notifierPlugin.Name} ({notifierPlugin.Description})'");
            prompt.AppendLine("```");
            prompt.AppendLine(message);
            prompt.AppendLine("```");

            prompt.AppendLine(@"
When responding, follow these rulse:
- Only call the plugins that are listed above, in the format shown above (1 line per plugin)!
- Fill out all parameters sensibly (everything inside {}).
  If not all parameters can be filled out, do not return that plugin.
  Plugins may have no parameters, in that case simply return the plugin name, as shown above.
- You may only return the same plugin multiple times if each instance has different parameters.
- Do not, under any circumstances, in any way explain the result you give!
");



            return prompt.ToString();
        }

        private string GenerateOutputPrompt(string promptHistory, INotifierPlugin sender, string request, string pluginResults)
        {
            StringBuilder prompt = new($"You are a personal digital assistant called Conrad.\n");

            prompt.AppendLine();
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine("Here is the message history:");
            prompt.AppendLine(promptHistory);
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine();

            prompt.AppendLine("Here is some background information. Use it only if you need it for the request.");
            foreach (var plugin in _pluginLoader.GetPlugins<IPromptAdderPlugin>())
            {
                prompt.AppendLine($" - {plugin.Name}:");
                prompt.AppendLine(plugin.PromptAddOn.Trim());
            }

            prompt.AppendLine();
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine();

            prompt.AppendLine("In a previous stage you executed plugins to address a request. Write an answer to that request.");
            prompt.AppendLine("Answer in full sentences, the output will be the input for a text to speech system. Therefore do not use abbreviations and write units in full words like degrees Celsius and Fahrenheit.");
            prompt.AppendLine("Only answer with the absolute nessesary information. Keep the answer short and to the point.");
            prompt.AppendLine("These are the results of the plugins that were executed to address the request:");
            prompt.AppendLine(pluginResults);

            prompt.AppendLine();
            prompt.AppendLine("--------------------------------------------------------");
            prompt.AppendLine();

            prompt.Append("Generate an answer to the request. Consider the plugin results above that were executed to address this request. The request was received from ");
            prompt.AppendLine($" {sender.Name} ({sender.Description}):\n```\n{request}\n```\n");
            prompt.AppendLine("Only use information you need to fulfill the request. Keep the answer short!");
            return prompt.ToString();

        }

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;

        private readonly IEnumerable<IOutputPlugin> _outputPlugins;

        private readonly ILangaugeModel _llm;

        private const int MAX_PROMPT_HISTORY = 2;
        private List<(DateTime, string)> _promptHistory = new(MAX_PROMPT_HISTORY * 2);
    }
}
