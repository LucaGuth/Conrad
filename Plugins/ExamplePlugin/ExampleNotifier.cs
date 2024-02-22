using PluginInterfaces;

namespace ExamplePluginPackage
{
    internal class ExampleNotifier : INotifierPlugin
    {
        public string Name { get; } = nameof(ExampleNotifier);

        public string Description { get; } = "This is an example notifier plugin.";

        public event NotifyEventHandler? OnNotify;

        public void Run()
        {
            while (true)
            {
                OnNotify?.Invoke(this, "Hello Event Raised from Example Notifier");
                Task.Delay(3000).Wait();
            }
        }
    }
}
