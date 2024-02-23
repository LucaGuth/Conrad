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

        public IEnumerable<IPlugin> GetPluginsOfType(Type type)
        {
            if (!type.IsAssignableFrom(type))
            {
                throw new ArgumentOutOfRangeException(type.ToString(), $"The requested plugin must be based on {nameof(IPlugin)}");
            }

            var plugins = _plugins.Where(p => type.IsAssignableFrom(p.GetType()));
            return plugins;
        }

        public PluginLoader(string pluginFolder, string configFilePath)
        {
            _configFilePath = configFilePath;
            _pluginFolder = pluginFolder;
            pluginFolder ??= Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");

            LoadPlugins(pluginFolder);

            if (File.Exists(configFilePath))
            {
                var config = File.ReadAllText(configFilePath);
                LoadConfig(config);
            }
            else
            {
                File.WriteAllText(configFilePath, GenerateConfig());
            }
        }

        #endregion

        #region Private
        private void LoadPluginFromFile(string pluginPath)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
            Type[] types = pluginAssembly.GetTypes();
            List<IPlugin> plugins = [];

            types.Where(t => t.GetInterface(PluginBaseInterfaceName.Name) != null && t.IsClass).ToList().ForEach(t =>
            {
                IPlugin plugin = (IPlugin)Activator.CreateInstance(t)!;
                plugins.Add(plugin);
            });

            _plugins.AddRange(plugins);
        }

        private void LoadPluginsFromDirectory(string pluginFolderPath)
        {
            if (!Directory.Exists(pluginFolderPath))
            {
                Directory.CreateDirectory(pluginFolderPath);
            }

            foreach (var file in Directory.GetFiles(pluginFolderPath, "*.dll"))
            {
                LoadPluginFromFile(file);
            }
        }

        private string GenerateConfig(IEnumerable<PluginConfig>? additionlConfigs = null)
        {
            IEnumerable<IConfigurablePlugin> configurablePlugins = GetPluginsOfType(typeof(IConfigurablePlugin)).Cast<IConfigurablePlugin>();
            List<PluginConfig> pluginConfigs = [];

            foreach (var configurablePlugin in configurablePlugins)
            {
                pluginConfigs.Add(new PluginConfig(configurablePlugin.GetType().AssemblyQualifiedName!, configurablePlugin.GetConfig()));
            }

            if (additionlConfigs is not null)
            {
                pluginConfigs.AddRange(additionlConfigs);
            }


            return JsonSerializer.Serialize(pluginConfigs, jsonSerializerOptions);
        }

        private void LoadConfig(string config)
        {
            List<PluginConfig> configWithoutPlugins = new();

            var loadedConfig = JsonSerializer.Deserialize<PluginConfig[]>(config) ?? throw new InvalidDataException("The configuration file is not valid");
            foreach (var pluginConfig in loadedConfig)
            {
                Type? type = Type.GetType(pluginConfig.PluginClassName);
                if (type is not null)
                {
                    var plugin = _plugins.First(p => type.IsAssignableFrom(p.GetType())) as IConfigurablePlugin;
                    plugin?.LoadConfig(pluginConfig.Config);
                }
                else
                {
                    configWithoutPlugins.Add(pluginConfig);
                }
            }

            if (configWithoutPlugins.Count > 0 || GetPluginsOfType(typeof(IConfigurablePlugin)).Count() > loadedConfig.Length)
            {
                var configTest = GenerateConfig(configWithoutPlugins);
                File.WriteAllText(_configFilePath, configTest);
            }
        }

        private readonly List<IPlugin> _plugins = [];
        private void LoadPlugins(string pluginPath)
        {
            LoadPluginsFromDirectory(pluginPath);
        }

        private readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

        private string _configFilePath;

        private string _pluginFolder;

        #endregion
    }

    [Serializable]
    internal struct PluginConfig(string type, JsonNode pluginConfig)
    {
        [JsonPropertyName("PluginName")]
        public string PluginClassName { get; set; } = type;

        [JsonPropertyName("PluginConfig")]
        public JsonNode Config { get; set; } = pluginConfig;
    }
}
