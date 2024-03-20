using PluginInterfaces;
using Serilog;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UserInputNotifierPackage
{
    public class UserInputNotifier : INotifierPlugin, IConfigurablePlugin
    {
        public string Name { get; } = "User Input";

        public string Description { get; } = "This plugin notifies of direct user input.";

        public void Run()
        {
            TcpListener server = new TcpListener(IPAddress.Any, config.tcpPort);

            server.Start();
            Log.Information("[{Name}] listening on {endpoint}", Name, server.LocalEndpoint);

            Byte[] bytes = new Byte[512];
            StringBuilder promptBuilder = new StringBuilder();

            while(true)
            {
                using TcpClient client = server.AcceptTcpClient();
                Log.Information("[{Name}] connection from {endpoint}", Name, client.Client.RemoteEndPoint);

                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = config.tcpReadTimeoutInMs;

                promptBuilder.Clear();

                while (true)
                {
                    int byteCount;
                    try {
                        byteCount = stream.Read(bytes, 0, bytes.Length);
                    }
                    catch (IOException) {
                        break;
                    }
                    if (byteCount == 0) break;
                    promptBuilder.Append(System.Text.Encoding.UTF8.GetString(bytes, 0, byteCount));
                }

                String prompt = promptBuilder.ToString();
                if (prompt.Length > 0)
                {
                    Log.Information("[{Name}] received prompt: {prompt}", Name, prompt);
                    OnNotify?.Invoke(this, prompt);
                }
                else {
                    Log.Information("[{Name}] no prompt received (timed out)", Name);
                }
            }
        }

        public event NotifyEventHandler? OnNotify;

        public event ConfigurationChangeEventHandler? OnConfigurationChange;

        public void LoadConfiguration(JsonNode settings)
        {
            config = settings.Deserialize<Config>() ?? throw new InvalidDataException("The config could not be loaded.");;
        }

        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(config);
            var jsonNode = JsonNode.Parse(localConfig)!;
            return jsonNode;
        }

        private Config config = new Config();

    }

    [Serializable]
    internal class Config
    {
        public int tcpPort { get; set; } = 4000;
        public int tcpReadTimeoutInMs { get; set; } = 1000;
    }
}
