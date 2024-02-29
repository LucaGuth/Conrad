using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using PluginInterfaces;
using Serilog;

namespace Sequencer
{
    /// <summary>
    /// The class that is responsible for loading and managing the plugins.
    /// </summary>
    internal class PluginLoader
    {
        #region Public
        /// <summary>
        /// The name of the interface that all plugins must implement.
        /// </summary>
        public static readonly Type PluginBaseInterfaceName = typeof(IPlugin);

        /// <summary>
        /// Filters the plugins by the type of the plugin.
        /// </summary>
        /// <param name="type">The type to filter the plugins by.</param>
        /// <returns>The list of plugins having the requested type.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the Requested type is not based on <see cref="ArgumentOutOfRangeException"/></exception>
        public IEnumerable<IPlugin> GetPluginsOfType(Type type)
        {
            if (!type.IsAssignableFrom(type))
            {
                throw new ArgumentOutOfRangeException(type.ToString(), $"The requested plugin must be based on {nameof(IPlugin)}");
            }

            var plugins = _plugins.Where(p => type.IsAssignableFrom(p.GetType()));
            Log.Verbose("Filtered Plugins of type {type}: {plugins}", type.Name, plugins);
            return plugins;
        }

        /// <summary>
        /// Creates a new PluginLoader instance.
        /// </summary>
        /// <param name="pluginFolder">The folder to load the plugins from.</param>
        /// <param name="configFilePath">The location of the configuration file.</param>
        public PluginLoader(string pluginFolder, string configFilePath)
        {
            _configFilePath = configFilePath;
            _pluginFolder = pluginFolder;

            Log.Information("Loading Plugins from {pluginFolder}", pluginFolder);

            LoadPlugins(pluginFolder);

            if (File.Exists(configFilePath))
            {
                Log.Information("Reading configuration file {configFilePath}", configFilePath);
                var config = File.ReadAllText(configFilePath);
                Log.Verbose("Configuration File Content:" + Environment.NewLine + " {config}", config);
                LoadConfig(config);
            }
            else
            {
                Log.Warning("The configuration file {configFilePath} does not exist. It will be created.", configFilePath);
                File.WriteAllText(configFilePath, GenerateConfig());
            }
        }

        /// <summary>
        /// Updates the configuration file with the current configuration of the plugins.
        /// </summary>
        public void UpdateConfiguration()
        {
            var updatedConfig = GenerateConfig(configWithoutPlugins);
                    Log.Information("The new configuration file will be written to {configFilePath}", _configFilePath);
                    File.WriteAllText(_configFilePath, updatedConfig);
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
                Log.Information("Loading Plugin {plugin}", t.Name);
                try
                {
                    IPlugin plugin = (IPlugin)Activator.CreateInstance(t)!;
                    plugins.Add(plugin);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error loading Plugin {plugin}", t.Name);
                }
            });

            _plugins.AddRange(plugins);
        }

        private void LoadPluginsFromDirectory(string pluginFolderPath)
        {
            if (!Directory.Exists(pluginFolderPath))
            {
                Log.Warning("The Plugin Folder {pluginFolderPath} does not exist. It will be created.", pluginFolderPath);
                Directory.CreateDirectory(pluginFolderPath);
            }

            var pluginFiles = Directory.GetFiles(pluginFolderPath, "*.dll");

            if (pluginFiles.Length == 0) Log.Warning("No Plugins found in {pluginFolderPath}", pluginFolderPath);
            else Log.Information("Found {pluginCount} plugin assemblies in {pluginFolderPath}", pluginFiles.Length, pluginFolderPath);

            foreach (var file in pluginFiles)
            {
                Log.Information("Loading Plugin from {file}", file);
                LoadPluginFromFile(file);
            }
        }

        private string GenerateConfig(IEnumerable<PluginConfig>? additionlConfigs = null)
        {
            IEnumerable<IConfigurablePlugin> configurablePlugins = GetPluginsOfType(typeof(IConfigurablePlugin)).Cast<IConfigurablePlugin>();
            List<PluginConfig> pluginConfigs = [];

            foreach (var configurablePlugin in configurablePlugins)
            {
                pluginConfigs.Add(new PluginConfig(configurablePlugin.GetType().AssemblyQualifiedName!, configurablePlugin.GetConfigiguration()));
            }

            if (additionlConfigs is not null)
            {
                pluginConfigs.AddRange(additionlConfigs);
            }

            return JsonSerializer.Serialize(pluginConfigs, jsonSerializerOptions);
        }

        readonly List<PluginConfig> configWithoutPlugins = [];

        private void LoadConfig(string config)
        {
            try
            {
                var loadedConfig = JsonSerializer.Deserialize<PluginConfig[]>(config) ?? throw new InvalidDataException("The configuration file is not valid");
                foreach (var pluginConfig in loadedConfig)
                {
                    Type? type = Type.GetType(pluginConfig.PluginClassName);
                    if (type is not null)
                    {
                        var plugin = _plugins.First(p => type.IsAssignableFrom(p.GetType())) as IConfigurablePlugin;
                        plugin?.LoadConfiguration(pluginConfig.Config);
                        Log.Information("Loaded Configuration for {plugin}", plugin?.GetType().Name);
                    }
                    else
                    {
                        configWithoutPlugins.Add(pluginConfig);
                        Log.Warning("The plugin {plugin} has a configuration entry but could not be found.", pluginConfig.PluginClassName);
                        Log.Verbose("The configuration entry: {pluginConfig}", pluginConfig.Config);
                    }
                }

                if (configWithoutPlugins.Count > 0 || GetPluginsOfType(typeof(IConfigurablePlugin)).Count() > loadedConfig.Length)
                {
                    Log.Warning("The configuration file contains entries for plugins that could not be found or new plugins were added. The configuration file will be updated.");
                    UpdateConfiguration();
                }
            }
            catch (Exception)
            {
                Log.Error("Error loading configuration file, utilizing default values for plugin settings! Please repair the currupted configuration file or remove it to generate a new one! You can run the program with the '{generateConfig}' flag to create a new one. Use '{help}' for more information.", "--generate-config", "--help");
            }
        }

        private readonly List<IPlugin> _plugins = [];
        private void LoadPlugins(string pluginPath)
        {
            LoadPluginsFromDirectory(pluginPath);
        }

        private readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

        private readonly string _configFilePath;

        private readonly string _pluginFolder;

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
