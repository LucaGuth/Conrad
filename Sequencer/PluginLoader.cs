using PluginInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sequencer
{
    internal static class PluginLoader
    {
        public static readonly string PluginBaseInterfaceName = "IPlugin";

        private static List<IPlugin> _plugins = new();

        public static void LoadPluginFromFile(string pluginPath)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
            Type[] types = pluginAssembly.GetTypes();
            List<IPlugin> plugins = new();

            types.Where(t => t.GetInterface(PluginBaseInterfaceName) != null && t.IsClass).ToList().ForEach(t =>
            {
                IPlugin plugin = (IPlugin)Activator.CreateInstance(t)!;
                plugins.Add(plugin);
            });

            _plugins.AddRange(plugins);
        }
        public static void LoadPluginsFromDirectory(string pluginFolderPath)
        {
            List<IPlugin> plugins = new List<IPlugin>();
            if (!Directory.Exists(pluginFolderPath))
            {
                Directory.CreateDirectory(pluginFolderPath);
            }

            foreach (var file in Directory.GetFiles(pluginFolderPath, "*.dll"))
            {
                LoadPluginFromFile(file);
            }
        }

        public static void LoadPlugins()
        {
            string pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            LoadPluginsFromDirectory(pluginPath);
        }

        public static IEnumerable<IExecutorPlugin> GetExecutorPlugins()
        {
            var executors = _plugins.Where(p => p is IExecutorPlugin);
            return executors.Cast<IExecutorPlugin>();
        }
        public static IEnumerable<INotifierPlugin> GetNotifierPlugins()
        {
            var notifier = _plugins.Where(p => p is INotifierPlugin);
            return notifier.Cast<INotifierPlugin>();
        }
    }
}
