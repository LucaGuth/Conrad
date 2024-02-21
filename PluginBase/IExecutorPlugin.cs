namespace PluginInterfaces
{
    public interface IExecutorPlugin : IPlugin
    {
        public string ParameterFormat { get; }
        public string Execute(string parameter);
    }
}
