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

            foreach (var notifier in _notifierPlugins)
            {
                Log.Debug("Subscribing to {PluginName}", notifier.Name);
                notifier.OnNotify += OnNotify;
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
            while (true);
        }

        private readonly NotifyEventHandler OnNotify = (INotifierPlugin sender, string message) =>
        {
            Log.Information("Received message from {PluginName}: {Message}", sender.Name, message);
        };

        private readonly PluginLoader _pluginLoader;

        private readonly IEnumerable<INotifierPlugin> _notifierPlugins;
    }
}
