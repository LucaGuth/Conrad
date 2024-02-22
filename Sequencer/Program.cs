using System.Reflection;
using Sequencer;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            string pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            PluginLoader.LoadPluginsFromDirectory(pluginPath);

            var sequence = new Sequence(PluginLoader.GetNotifierPlugins(), PluginLoader.GetExecutorPlugins());

            sequence.Run();
        }
    }
}