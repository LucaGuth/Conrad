using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PluginInterfaces;


namespace Sequencer
{
    internal class PluginLoader
    {
        public static readonly string PluginBaseInterfaceName = "IPlugin";

        private List<IPlugin> _plugins = new();

        public void LoadPluginFromFile(string pluginPath)
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
        public void LoadPluginsFromDirectory(string pluginFolderPath)
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

        private void LoadPlugins()
        {
            string pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            LoadPluginsFromDirectory(pluginPath);
        }

        public PluginLoader(string? pluginPath = null)
        {
            if (pluginPath == null)
            {
                pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            }

            LoadPlugins();
        }

        public IEnumerable<IPlugin> GetPluginsOfType(Type type)
        {
            if (!type.IsAssignableFrom(type))
            {
                throw new ArgumentOutOfRangeException(type.ToString(), $"The requested plugin must be based on {nameof(IPlugin)}");
            }

            var plugins = _plugins.Where(p => type.IsAssignableFrom(p.GetType()));
            return plugins;
        }

        public string GenerateConfig()
        {
            IEnumerable<IConfigurablePlugin> configurablePlugins = GetPluginsOfType(typeof(IConfigurablePlugin)).Cast<IConfigurablePlugin>();
            IList<PluginConfig> pluginConfigs = new List<PluginConfig>();

            foreach (var configurablePlugin in configurablePlugins)
            {
                pluginConfigs.Add(new PluginConfig(configurablePlugin.GetType().AssemblyQualifiedName, configurablePlugin.GetConfig()));
            }


            return JsonSerializer.Serialize(pluginConfigs, jsonSerializerOptions);
        }

        private JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        public void LoadConfig(string config)
        {
            var loadedConfig = JsonSerializer.Deserialize<PluginConfig[]>(config);

            foreach (var pluginConfig in loadedConfig)
            {
                Type type = Type.GetType(pluginConfig.PluginClassName)!;

                //if (!type.IsSubclassOf(typeof(IConfigurablePlugin)))
                //{
                //    throw new InvalidOperationException($"The Configuration of {type.FullName} is not possible because it does not Implement {nameof(IConfigurablePlugin)}");
                //}

                var plugin = _plugins.First(p => type.IsAssignableFrom(p.GetType())) as IConfigurablePlugin;

                if ( plugin is not null)
                {
                    plugin.LoadConfig(pluginConfig.Config);
                }
            }
        }
    }

    [Serializable]
    internal struct PluginConfig
    {
        [JsonPropertyName("PluginName")]
        public string PluginClassName { get; set; }

        [JsonPropertyName("PluginConfig")]
        public JsonNode Config { get; set; }

        public PluginConfig(string type, JsonNode pluginConfig)
        {
            PluginClassName = type;
            Config = pluginConfig;
        }
    }
}
