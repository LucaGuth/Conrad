using PluginInterfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamplePluginPackage
{
    [Serializable]
    public class ExampleNotifier2 : INotifierPlugin, IConfigurablePlugin
    {
        public string Name { get; } = "Example Notifier 2";

        public string Description { get; } = "This is a second example notifier plugin.";


        public int SpecialConfig { get; set; }
        public JsonNode GetConfig()
        {
            var localConfig = JsonSerializer.Serialize(this);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public event NotifyEventHandler? OnNotify;

        public void LoadConfig(JsonNode settings)
        {
            var deseri = settings.Deserialize<ExampleNotifier2>() ?? throw new InvalidDataException("The config could not be loaded.");
            SpecialConfig = deseri.SpecialConfig;
        }

        public void Run()
        {
            while (true)
            {
                OnNotify?.Invoke(this, $"Hello My Value: {SpecialConfig}");
                Task.Delay(3000).Wait();
            }
        }
    }
}
