using PluginInterfaces;
using Serilog;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

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

            _outputPlugins = _pluginLoader.GetPlugins<IOutputPlugin>();
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
            var response = message;

            Log.Information("[Sequencer] final response: {response}", response);

            // output to user
            Parallel.ForEach(_outputPlugins, outputPlugin => outputPlugin.PushMessage(response));
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

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;

        private readonly IEnumerable<IOutputPlugin> _outputPlugins;
    }
}
