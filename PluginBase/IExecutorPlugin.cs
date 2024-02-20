namespace PluginInterfaces
{
    public interface IExecutorPlugin : IPlugin
    {
        public string Execute(string parameter);
    }
}
