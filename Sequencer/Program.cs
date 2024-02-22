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

            var pluginLoader = new PluginLoader();


            //File.WriteAllText("config.json", pluginLoader.GenerateConfig());

            var configString = File.ReadAllText("config.json");

            pluginLoader.LoadConfig(configString);

            var sequence = new Sequence(pluginLoader);
            sequence.Run();

        }
    }
}