using PluginInterfaces;

namespace ExamplePlugin
{
    public class ExampleExecutor : IExecutorPlugin
    {
        public string Name { get => nameof(ExampleExecutor); }

        public string Execute(string parameter)
        {
            throw new NotImplementedException();
        }
    }
}
