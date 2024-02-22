namespace PluginInterfaces
{
    public interface INotifierPlugin : IPlugin
    {
        public event NotifyEventHandler OnNotify;

        public void Run();
    }

    public delegate void NotifyEventHandler(INotifierPlugin sender, string message);
}
