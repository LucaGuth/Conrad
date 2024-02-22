using PluginInterfaces;

namespace ExamplePlugin
{
    public class ExampleExecutor : IExecutorPlugin
    {
        public string Name { get; } = nameof(ExampleExecutor);

        public string ParameterFormat { get; } = "No special order";

        public string Description { get; } = "This is an example executor plugin.";

        public string Execute(string parameter)
        {
            Task.Delay(3000).Wait();
            return "Example Executor Executed!";
        }
    }
}
