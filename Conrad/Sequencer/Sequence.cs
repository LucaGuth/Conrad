using PluginInterfaces;
using Serilog;

namespace Sequencer
{
    internal class Sequence
    {
        public Sequence(PluginLoader pluginLoader)
        {
            Log.Information("Initializing Sequence");
            _pluginLoader = pluginLoader;

            Log.Information("Loading Notifier Plugins");
            _notifierPlugins = _pluginLoader.GetPluginsOfType(typeof(INotifierPlugin)).Cast<INotifierPlugin>();

            NotifyEventHandler OnNotify = (INotifierPlugin sender, string message) =>
            {
                Log.Information("Received message from {PluginName}: {Message}", sender.Name, message);
            };

            foreach (var notifier in _notifierPlugins)
            {
                Log.Debug("Subscribing to notifications to {PluginName}", notifier.Name);
                notifier.OnNotify += OnNotify;
            }

            ConfigurationChangeEventHandler OnConfigurationChange = (IConfigurablePlugin plugin) =>
            {
                _pluginLoader.UpdateConfiguration();
            };

            Log.Information("Loading Configurable Plugins");
            var configurablePluigns = _pluginLoader.GetPluginsOfType(typeof(IConfigurablePlugin)).Cast<IConfigurablePlugin>();
            foreach (var configurablePlugin in configurablePluigns)
            {
                Log.Debug("Subscribing to Configuration Change Event for Plugin {PluginName}", configurablePlugin.Name);
                configurablePlugin.OnConfigurationChange += OnConfigurationChange;
            }

        }

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

            Log.Information("Sequence Running");
            while (true) ;
        }

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;
    }
}
