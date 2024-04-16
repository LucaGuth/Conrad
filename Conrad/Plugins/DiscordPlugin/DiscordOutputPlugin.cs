using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;

using PluginInterfaces;
using System.Text;

namespace DiscordOutputPlugin
{
    public class DiscordOutputPlugin : IOutputPlugin, IConfigurablePlugin
    {
        #region public
        public string Name => nameof(DiscordOutputPlugin);

        public string Description => "Plugin for Text-to-Speech";

        public event ConfigurationChangeEventHandler? OnConfigurationChange;

        public void LoadConfiguration(JsonNode settings)
        {
            config = settings.Deserialize<PiperOutputPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
        }
        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public void PushMessage(string message)
        {
            message = String.Join("", message.Split('*'));

            PiperProcess.Start();
            PiperProcess.StandardInput.WriteLine(message);
            PiperProcess.StandardInput.Close();
            var a = PiperProcess.StandardOutput.BaseStream;
            PiperProcess.WaitForExit();
            if (PiperProcess.ExitCode != 0)
            {
                Log.Warning("[Discord]: piper exited with code: {ExitCode}, piper args: {Args}", PiperProcess.ExitCode, PiperProcess.StartInfo.Arguments);
            }

            var audioFile = File.ReadAllBytes(_piperTempAudioFile);

            var textRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(config.DiscordBotEndpoint),
            };
            textRequest.Content = new StringContent(message, new MediaTypeHeaderValue("text/plain"));

            var audioRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(config.DiscordBotEndpoint),
            };


            audioRequest.Content = new ByteArrayContent(audioFile);
            audioRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");

            try
            {
                var textTask = _httpClient.SendAsync(textRequest);
                var audioTask = _httpClient.SendAsync(audioRequest);

                textTask.Wait();
                audioTask.Wait();
            }
            catch (Exception e)
            {
                Log.Error(e, "[Discord]: Error while sending data to the Discord Bridge.");
            }
        }

        public void Initialize()
        {
            InitializePiper();
        }

        void injectHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        #endregion

        #region private
        private void InitializePiper()
        {
            PiperProcess.StartInfo.FileName = config.PiperPath;
            PiperProcess.StartInfo.UseShellExecute = false;
            PiperProcess.StartInfo.RedirectStandardInput = true;
            PiperProcess.StartInfo.RedirectStandardOutput = true;
            PiperProcess.StartInfo.RedirectStandardError = true;
            PiperProcess.StartInfo.CreateNoWindow = true;
            PiperProcess.ErrorDataReceived += (sender, e) => Log.Debug("[Discord] [TTS]: {Piper Message}", e.Data);
            PiperProcess.OutputDataReceived += (sender, e) => Log.Debug("[Discord] [TTS]: {Piper Message}", e.Data);

            PiperProcess.StartInfo.Arguments = "--version";
            PiperProcess.Start();
            PiperProcess.WaitForExit();
            _piperTempAudioFile = Path.Combine(config.TempPath, "tts.ogg");
            if (!Directory.Exists(config.TempPath))
            {
                Directory.CreateDirectory(config.TempPath);
            }
            PiperProcess.StartInfo.Arguments = $"--model {config.ModelPath} --config {config.ConfigPath} --length-scale {config.LengthScale.ToString("0.00", CultureInfo.InvariantCulture)} --output_file {_piperTempAudioFile}";
        }


        private void InitializeDiscord()
        {
            throw new NotImplementedException();
        }

        private PiperOutputPluginConfig config = new();

        private readonly Process PiperProcess = new();

        private HttpClient _httpClient = new();

        private string _piperTempAudioFile = "";

        #endregion
    }

    [Serializable]
    public class PiperOutputPluginConfig
    {
        public string PiperPath { get; set; } = "/python_venv/bin/piper";
        public string ModelPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx";
        public string ConfigPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx.json";
        public string TempPath { get; set; } = "./tmp";
        public string DiscordBotEndpoint { get; set; } = "http://test";
        public double LengthScale { get; set; } = 1.0;
    }
}
