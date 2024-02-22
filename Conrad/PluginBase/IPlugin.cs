using System.Text.Json.Serialization;

namespace PluginInterfaces
{
    public interface IPlugin
    {
        public string Name { get; }

        public string Description { get; }
    }
}
