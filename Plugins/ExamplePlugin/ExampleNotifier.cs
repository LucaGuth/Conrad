using PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExamplePluginPackage
{
    internal class ExampleNotifier : INotifierPlugin
    {
        public string Name { get; } = nameof(ExampleNotifier);

        public string Description { get; } = "This is an example notifier plugin.";

        public event NotifyEventHandler? OnNotify;

        public Task Run()
        {
            while (true)
            {
                OnNotify?.Invoke(this, "Hello Event Raised from Example Notifier");
                Task.Delay(5000).Wait();
            }
        }
    }
}
