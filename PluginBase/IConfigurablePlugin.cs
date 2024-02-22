using System.Text.Json.Nodes;

namespace PluginInterfaces
{
    public interface IConfigurablePlugin : IPlugin
    {
        public JsonNode GetConfig();
        public void LoadConfig(JsonNode settings);
    }
}
