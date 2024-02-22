namespace PluginInterfaces
{
    public interface INotifierPlugin : IPlugin
    {
        public event NotifyEventHandler OnNotify;

        public Task Run();
    }

    public delegate void NotifyEventHandler(INotifierPlugin sender, string message);
}
