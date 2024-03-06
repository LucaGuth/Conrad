namespace PluginInterfaces
{
    public interface IExecutorPlugin : IPlugin
    {
        public string ParameterFormat { get; }
        public Task<string> Execute(string parameter);
    }
}
