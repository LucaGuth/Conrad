namespace PluginInterfaces
{
    /// <summary>
    /// The interface for plugins that are able to execute commands.
    /// </summary>
    public interface IExecutorPlugin : IPlugin
    {
        public string ParameterFormat { get; }

        /// <summary>
        /// The method that is called once the plugin needs to execute a command.
        /// </summary>
        /// <param name="parameter">The parameters needed for processing the request in YAML format.</param>
        /// <returns>The result of the request.</returns>
        public string Execute(string parameter);
    }
}
