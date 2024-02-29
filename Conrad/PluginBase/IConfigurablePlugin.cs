using System.Text.Json.Nodes;

namespace PluginInterfaces
{
    /// <summary>
    /// The interface for plugins that are configurable.
    /// </summary>
    public interface IConfigurablePlugin : IPlugin
    {
        /// <summary>
        /// The method that returns the current configuration of the plugin.
        /// </summary>
        /// <returns>The current configuration as JsonNode</returns>
        public JsonNode GetConfigiguration();

        /// <summary>
        /// The method that loads the configuration of the plugin.
        /// </summary>
        /// <param name="configuration">The configuration that will be applied as JsonNode</param>
        public void LoadConfiguration(JsonNode configuration);

        /// <summary>
        /// The event that is raised when the configuration of the plugin changes during runtime.
        /// </summary>
        public event ConfigurationChangeEventHandler OnConfigurationChange;
    }

    /// <summary>
    /// The delegate for the OnConfigurationChange event.
    /// </summary>
    /// <param name="plugin">The plugin that raised the event.</param>
    public delegate void ConfigurationChangeEventHandler(IConfigurablePlugin plugin);
}
