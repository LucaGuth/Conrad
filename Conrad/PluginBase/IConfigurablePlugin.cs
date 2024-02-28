using System.Text.Json.Nodes;

namespace PluginInterfaces
{
    public interface IConfigurablePlugin : IPlugin
    {
        public JsonNode GetConfigiguration();
        public void LoadConfiguration(JsonNode configuration);

        public event ConfigurationChangeEventHandler OnConfigurationChange;
    }

    public delegate void ConfigurationChangeEventHandler(IConfigurablePlugin plugin);
}
