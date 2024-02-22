using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using PluginInterfaces;

namespace Sequencer
{
    internal class PluginLoader
    {
        #region Public
        public static readonly Type PluginBaseInterfaceName = typeof(IPlugin);

        public void LoadPluginFromFile(string pluginPath)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
            Type[] types = pluginAssembly.GetTypes();
            List<IPlugin> plugins = new();

            types.Where(t => t.GetInterface(PluginBaseInterfaceName.Name) != null && t.IsClass).ToList().ForEach(t =>
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

        public PluginLoader(string pluginFolder, string configPath)
        {
            if (pluginFolder == null)
            {
                pluginFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            }

            LoadPlugins(pluginFolder);

            if (File.Exists(configPath))
            {
                var config = File.ReadAllText(configPath);
                LoadConfig(config);
            }
            else
            {
                File.WriteAllText(configPath, GenerateConfig());
            }
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

        public void LoadConfig(string config)
        {
            var loadedConfig = JsonSerializer.Deserialize<PluginConfig[]>(config);

            foreach (var pluginConfig in loadedConfig)
            {
                Type type = Type.GetType(pluginConfig.PluginClassName)!;

                var plugin = _plugins.First(p => type.IsAssignableFrom(p.GetType())) as IConfigurablePlugin;

                if ( plugin is not null)
                {
                    plugin.LoadConfig(pluginConfig.Config);
                }
            }
        }
        #endregion

        #region Private
        private List<IPlugin> _plugins = new();
        private void LoadPlugins(string pluginPath)
        {
            LoadPluginsFromDirectory(pluginPath);
        }

        private JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        #endregion
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
