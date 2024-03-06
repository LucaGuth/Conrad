using PluginInterfaces;
using System.Text.Json.Serialization;

namespace ExamplePlugin
{
    public class ExampleExecutor : IExecutorPlugin
    {
        public string Name { get; } = nameof(ExampleExecutor);

        public string ParameterFormat { get; } = "No special order";

        public string Description { get; } = "This is an example executor plugin.";

        public async Task<string> Execute(string parameter)
        {
            await Task.Delay(3000); // Asynchronously wait for 3 seconds
            return "Example Executor Executed!"; // This line executes after the delay
        }
    }
}
