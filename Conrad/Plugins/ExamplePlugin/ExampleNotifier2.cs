using PluginInterfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamplePluginPackage
{
    public class ExampleNotifier2 : INotifierPlugin, IConfigurablePlugin
    {
        public string Name { get; } = "Example Notifier 2";

        public string Description { get; } = "This is a second example notifier plugin.";
        public void Run()
        {
            while (true)
            {
                OnNotify?.Invoke(this, $"Hello My Value: {config.ImplementationSpecificVaiue}");

                Task.Delay(3000).Wait();
            }
        }

        public event NotifyEventHandler? OnNotify;

        public event ConfigurationChangeEventHandler? OnConfigurationChange;

        public void LoadConfiguration(JsonNode settings)
        {
            config = settings.Deserialize<ExampleNotifier2Configuration>() ?? throw new InvalidDataException("The config could not be loaded.");
        }
        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        private ExampleNotifier2Configuration config = new();
    }

    [Serializable]
    public class ExampleNotifier2Configuration
    {
        public int ImplementationSpecificVaiue { get; set; } = 0;
        public string[] Texts { get; set; } = ["hallo", "tschüssi", "hallihallo"];
    }
}
