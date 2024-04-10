namespace PluginInterfaces
{
    /// <summary>
    /// The interface for plugins that are able to invoke the sequence.
    /// </summary>
    public interface INotifierPlugin : IPlugin
    {
        /// <summary>
        /// The event that is raised when the plugin wants to notify the sequence.
        /// </summary>
        public event NotifyEventHandler OnNotify;

        /// <summary>
        /// The main loop of the plugin working in the background.
        /// </summary>
        public void Run();
    }

    /// <summary>
    /// The delegate for the OnNotify event.
    /// </summary>
    /// <param name="sender">The plugin that raised the event.</param>
    /// <param name="message">The notification Message.</param>
    public delegate void NotifyEventHandler(INotifierPlugin sender, string message);
}
