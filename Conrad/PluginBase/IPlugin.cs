namespace PluginInterfaces
{
    /// <summary>
    /// The base interface for all plugins containing basic information about the plugin.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// The name of the plugin.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Description of the plugin.
        /// </summary>
        public string Description { get; }
    }
}
