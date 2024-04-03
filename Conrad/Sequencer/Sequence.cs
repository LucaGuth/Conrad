using PluginInterfaces;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

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
        private void OnNotify(INotifierPlugin sender, string message) {
            Log.Information("[Sequencer] Received message from {PluginName}: {Message}", sender.Name, message);

            // llm
            // executor plugins
            // llm
            var promt = GenerateInputPromt(sender, message);
            Log.Debug("Sending promt to LLM: {promt}", promt);
            var response = _llm.Process(promt);

            Log.Information("[Sequencer] final response: {response}", response);

            CancellationTokenSource cts = new CancellationTokenSource();

            // output to user
            List<Task> outputTasks = new List<Task>();
            object lockOutputTasks = new object();
            foreach (var outputPlugin in _outputPlugins)
            {
                var task = new Task(() => outputPlugin.PushMessage(response), cts.Token);
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
                if (time.ElapsedMilliseconds > 30000)
                {
                    Log.Warning("Output Plugins are taking too long to respond. Cancelling tasks: {tasks}", outputTasks);
                    cts.Cancel();
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
You are a personal digital assistant. You are designed to help people with their daily tasks. Your actions are split into plugins.
";
            StringBuilder promt = new StringBuilder(_llmPromtHeader);
            promt.AppendLine("You have the following features:");
            foreach (var plugin in _pluginLoader.GetPlugins<IExecutorPlugin>())
            {
                promt.AppendLine($"- {plugin.Name} (): {plugin.Description}");
                promt.AppendLine($"\tThe parameter format is {plugin.ParameterFormat}");
                promt.AppendLine();
            }

            promt.AppendLine($"You have received a message from {notifierPlugin.Name} ({notifierPlugin.Description})\"{message}\"");
            promt.AppendLine("Choose which plugins and fill out corresponding parameters. If you cannot fill out parameters from the request, do not return the plugin. Return only necessary plugins and no others.");
            promt.AppendLine("Return them in a machine readable format:");
            promt.AppendLine("-PluginName(Patameters)");
            promt.AppendLine("-SecondPluignName(OthersParameters)");
            promt.AppendLine("If no plugin fits the request return an empty list.");
            return promt.ToString();
        }

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;

        private readonly IEnumerable<IOutputPlugin> _outputPlugins;

        private readonly ILangaugeModel _llm;
    }
}
