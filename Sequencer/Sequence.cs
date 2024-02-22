using PluginInterfaces;
using System.Linq;
using System.Text.Json;
using System.Xml;

namespace Sequencer
{
    internal class Sequence
    {
        public Sequence(PluginLoader pluginLoader)
        {
            _pluginLoader = pluginLoader;

            _notifierPlugins = _pluginLoader.GetPluginsOfType(typeof(INotifierPlugin)).Cast<INotifierPlugin>();
            _executorPlugins = _pluginLoader.GetPluginsOfType(typeof(IExecutorPlugin)).Cast<IExecutorPlugin>();

            foreach (var notifier in _notifierPlugins)
            {
                notifier.OnNotify += OnNotify;
            }
        }

        public void Run()
        {
            List<Task> tasks = new List<Task>();
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

        private NotifyEventHandler OnNotify = (INotifierPlugin sender, string message) =>
        {
            Console.WriteLine($"Received message from {sender.Name}: {message}");
        };

        private PluginLoader _pluginLoader;

        private IEnumerable<IExecutorPlugin> _executorPlugins;
        private IEnumerable<INotifierPlugin> _notifierPlugins;
    }
}
