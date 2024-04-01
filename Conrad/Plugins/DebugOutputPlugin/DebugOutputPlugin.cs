using PluginInterfaces;
using Serilog;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DebugOutputPluginPackage
{
    public class DebugOutputPlugin : IOutputPlugin, IConfigurablePlugin
    {
        #region public
        public string Name => nameof(DebugOutputPlugin);

        public string Description => "Plugin for Debug purpurses";

        public event ConfigurationChangeEventHandler? OnConfigurationChange;

        public void LoadConfiguration(JsonNode settings)
        {
            config = settings.Deserialize<DebugOutputPluginConfiguration>() ?? throw new InvalidDataException("The config could not be loaded.");
        }
        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public void PushMessage(string message)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);

            try
            {
                using TcpClient client = new(AddressFamily.InterNetwork);
                client.Connect(config.Hostname, config.Port);
                //using TcpClient client = new TcpClient(endpoint.Hostname, endpoint.Port);
                using NetworkStream stream = client.GetStream();
                stream.WriteTimeout = config.WriteTimeoutInMs;
                stream.Write(bytes);
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                Log.Error("[{Name}] Could not send response to {Hostname}:{Port}", Name, config.Hostname, config.Port, e);
            }

        }

        #endregion

        #region private

        private DebugOutputPluginConfiguration config = new();

        #endregion
    }

    [Serializable]
    public class DebugOutputPluginConfiguration
    {
        public string Hostname { get; set; } = "";
        public int Port { get; set; } = 4001;
        public int WriteTimeoutInMs { get; set; } = 2000;
    }
}
