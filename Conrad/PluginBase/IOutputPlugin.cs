namespace PluginInterfaces
{
    /// <summary>
    /// The interface for output plugins.
    /// </summary>
    public interface IOutputPlugin : IPlugin
    {
        /// <summary>
        /// The method that is called to push a message to the output.
        /// </summary>
        /// <param name="message">The message that will be sent to the client.</param>
        void PushMessage(string message);
    }
}
