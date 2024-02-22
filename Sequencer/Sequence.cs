using PluginInterfaces;

namespace Sequencer
{
    internal class Sequence
    {
        public Sequence(IEnumerable<INotifierPlugin> notifierPlugins, IEnumerable<IExecutorPlugin> executorPlugins)
        {
            _notifierPlugins = notifierPlugins;
            _executorPlugins = executorPlugins;

            foreach (var notifier in _notifierPlugins)
            {
                notifier.OnNotify += OnNotify;
            }
        }

        private NotifyEventHandler OnNotify = (INotifierPlugin sender, string message) =>
        {
            Console.WriteLine($"Received message from {sender.Name}: {message}");
        };

        private IEnumerable<IExecutorPlugin> _executorPlugins;
        private IEnumerable<INotifierPlugin> _notifierPlugins;

        public void Run()
        {
            foreach (var notifier in _notifierPlugins)
            {
                notifier.Run();
            }

            while (true);
        }
    }
}
