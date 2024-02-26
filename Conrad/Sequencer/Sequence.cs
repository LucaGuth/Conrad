using PluginInterfaces;
using Serilog;

namespace Sequencer
{
    internal class Sequence
    {
        public Sequence(PluginLoader pluginLoader)
        {
            _pluginLoader = pluginLoader;

            _notifierPlugins = _pluginLoader.GetPluginsOfType(typeof(INotifierPlugin)).Cast<INotifierPlugin>();

            foreach (var notifier in _notifierPlugins)
            {
                notifier.OnNotify += OnNotify;
            }
        }

        public void Run()
        {
            List<Task> tasks = [];
            foreach (var notifier in _notifierPlugins)
            {
                tasks.Add(new Task(notifier.Run));
            }

            foreach (var task in tasks)
            {
                task.Start();
            }

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
