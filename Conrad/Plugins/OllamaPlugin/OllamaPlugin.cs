using OllamaSharp;
using OllamaSharp.Models;
using PluginInterfaces;
using Serilog;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OllamaPluginPackage
{
    public class OllamaPlugin : ILangaugeModel, IConfigurablePlugin
    {
        #region public
        public string Name => nameof(OllamaPlugin);

        public string Description => "Plugin for Debug purpurses";

        public event ConfigurationChangeEventHandler? OnConfigurationChange;

        public void LoadConfiguration(JsonNode settings)
        {
            _config = settings.Deserialize<OllamaConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
            Log.Information("Loaded configuration for {Name}", Name);
            // set up the client
            var uri = new Uri(_config.Uri);
            _ollama = new OllamaApiClient(uri);

            Stopwatch stopwatch = Stopwatch.StartNew();

            var localModels = _ollama.ListLocalModels().Result;

            Log.Debug("Local Models: {LocalModels}", localModels);

            int currentPercentage = 0;

            if (!localModels.Where(model => model.Name == _config.Model).Any())
            {
                Log.Warning("Model {Model} not found. Downloading it now.", _config.Model);
                _ollama.PullModel(_config.Model, status =>
                {
                    int newPercentage = (int)status.Percent;

                    if (newPercentage > currentPercentage || stopwatch.ElapsedMilliseconds > 5000)
                    {
                        Log.Information($"[Ollama] ({status.Percent}%) {status.Status}");
                        currentPercentage = newPercentage;
                        stopwatch.Restart();
                    }
                }).Wait();
            }

           _ollama.SelectedModel = _config.Model;

        }
        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(_config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public string Process(string promt)
        {
            StringBuilder response = new();
            try
            {
                _ollama.StreamCompletion(promt, null, stream => response.Append(stream.Response)).Wait(_config.Timeout);
            }
            catch (Exception e)
            {
                Log.Error("[{Name}] Could not send response to {Hostname}:{Port}", Name, _config.Uri, e);
            }
            return response.ToString();
        }

        #endregion

        #region private

        private OllamaConfig _config = new();

        private OllamaApiClient _ollama;

        #endregion
    }

    [Serializable]
    public class OllamaConfig
    {
        public string Uri { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "gemma:latest";
        public int Timeout { get; set; } = 60000;
    }
}
