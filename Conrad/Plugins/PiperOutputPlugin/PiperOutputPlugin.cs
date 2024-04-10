using PluginInterfaces;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PiperOutputPlugin
{
    public class PiperOutputPlugin : IOutputPlugin, IConfigurablePlugin
    {
        #region public
        public string Name => nameof(PiperOutputPlugin);

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
            PiperProcess.WaitForExit();

            if (PiperProcess.ExitCode != 0) {
                Log.Warning("[Piper]: piper exited with code: {ExitCode}, piper args: {Args}", PiperProcess.ExitCode, PiperProcess.StartInfo.Arguments);
            }
        }

        public void Initialize()
        {
            PiperProcess.StartInfo.FileName = config.PiperPath;
            PiperProcess.StartInfo.UseShellExecute = false;
            PiperProcess.StartInfo.RedirectStandardInput = true;
            PiperProcess.StartInfo.RedirectStandardOutput = true;
            PiperProcess.StartInfo.RedirectStandardError = true;
            PiperProcess.StartInfo.CreateNoWindow = true;
            PiperProcess.ErrorDataReceived += (sender, e) => Log.Debug("[Piper]: {Piper Message}", e.Data);
            PiperProcess.OutputDataReceived += (sender, e) => Log.Debug("[Piper]: {Piper Message}", e.Data);

            PiperProcess.StartInfo.Arguments = "--version";
            PiperProcess.Start();
            PiperProcess.WaitForExit();
            PiperProcess.StartInfo.Arguments = $"--model {config.ModelPath} --config {config.ConfigPath} --length-scale {config.LengthScale.ToString("0.00", CultureInfo.InvariantCulture)} --output_file conrad_test_audio.wav";
        }

        #endregion

        #region private

        private PiperOutputPluginConfig config = new();

        readonly Process PiperProcess = new();

        #endregion
    }

    [Serializable]
    public class PiperOutputPluginConfig
    {
        public string PiperPath { get; set; } = "/python_venv/bin/piper";
        public string ModelPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx";
        public string ConfigPath { get; set; } = "/app/piper_models/en_US-ryan-high.onnx.json";

        public double LengthScale { get; set; } = 1.0;
    }
}
