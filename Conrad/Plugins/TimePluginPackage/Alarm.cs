using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using System.Runtime.CompilerServices;

namespace TimePluginPackage
{
    internal class Alarm : INotifierPlugin, IExecutorPlugin, IConfigurablePlugin, IPromptAdderPlugin
    {
        public string Name => "AlarmClock";

        public string Description => "Sets and removes and raises alarms.";

        public string ParameterFormat => @"Action:'{setAlarm/removeAlarm}', AlarmName:'{name}', Time:'{yyyy-MM-dd HH:mm:ss}'";

        public string PromptAddOn
        {
            get
            {
                StringBuilder promptAdder = new($"The current time is: {DateTime.Now.ToString("HH:mm")} on {DateTime.Now.ToString("dddd, dd of MMMM yyyy")}.\n");

                if (_config.Alarms.Any())
                {
                    foreach ((var alarmName, var alarmTime) in _config.Alarms)
                    {
                        promptAdder.AppendLine($"Alarm \"{alarmName}\" is set to {alarmTime.ToString("HH:mm")} on {alarmTime.ToString("yyyy-MM-dd")}");
                    }
                }
                else
                {
                    promptAdder.AppendLine("No Alarms are set.");
                }

                return promptAdder.ToString();
            }
        }

        private static readonly char[] TRIM_CHARS = { '\'', '\"', '{', '}', ',', '.', ' ' };

        public event ConfigurationChangeEventHandler OnConfigurationChange;

        public event NotifyEventHandler OnNotify;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<string> ExecuteAsync(string parameter)
        {
            Log.Debug("Start execution of the TimerPlugin");

            parameter = parameter.ToLower();
            Log.Debug("[AlarmClock]: Recieved Parameter {parameter}", parameter);
            var actionRegex = new Regex(@"((?:setalarm)|(?:removealarm))");
            var nameRegex = new Regex(@".*alarmname:([^,]*),?");
            var timeRegex = new Regex(@".*time:([^,]*),?");

            var action = actionRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);
            var alarmName = nameRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);

            StringBuilder actions = new StringBuilder();

            try
            {
                switch (action.Trim())
                {
                    case "setalarm":
                        var alarmTime = DateTime.Parse(timeRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS));
                        _config.Alarms[alarmName] = alarmTime;
                        var alarmAction = $"New Alarm \"{alarmName}\" was set to {alarmTime.ToString("HH:mm, d.")} {alarmTime.ToString("MMMM yyyy")}";
                        actions.AppendLine(alarmAction);
                        Log.Information("[AlarmClock]: Set Alarm {AlarmName} to {Time}", alarmName, alarmTime.ToString());
                        OnConfigurationChange?.Invoke(this); break;
                    case "removealarm":
                        if (_config.Alarms.Remove(alarmName))
                        {
                            Log.Information("[AlarmClock]: Removed Alarm {AlarmName}", alarmName);
                            actions.AppendLine($"Removed Alarm \"{alarmName}\" from the list. Tell the user that the alarm was sucessfully removed.");
                            OnConfigurationChange?.Invoke(this);
                        }
                        else
                        {
                            Log.Information("[AlarmClock]: Tried to remove non-existing Alarm {AlarmName}", alarmName);
                            actions.AppendLine($"Tried to remove non-existing Alarm \"{alarmName}\"");
                        }
                        break;
                    default:
                        actions.AppendLine($"Couldn't run action: {action} because it does not exist!");
                        Log.Information("[AlarmClock]: Tried to run non-existing action {Action}", action);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "[AlarmClock]: Error while executing action {Action}", action);
                actions.AppendLine($"Error while executing action {action}");
            }

            return Task.FromResult(actions.ToString());
        }

        public JsonNode GetConfigiguration()
        {
            var localConfig = JsonSerializer.Serialize(_config);
            var jsonNode = JsonNode.Parse(localConfig)!;

            return jsonNode;
        }

        public void LoadConfiguration(JsonNode configuration)
        {
            _config = configuration.Deserialize<AlarmPluginConfig>() ?? throw new InvalidDataException("The config could not be loaded.");
        }

        public void Run()
        {
            while (true)
            {
                var finishedAlarm = CheckAlarms();
                if (finishedAlarm is not null)
                {
                    var finishedAlarmName = finishedAlarm.Value.Key;
                    var finishedAlarmTime = finishedAlarm.Value.Value;
                    _config.Alarms.Remove(finishedAlarm.Value.Key);
                    OnNotify?.Invoke(this, $"The Alarm \"{finishedAlarmName}\" was raised. The time {finishedAlarmTime.ToString("HH:mm")} is over.");
                    OnConfigurationChange?.Invoke(this);
                }

                Task.Delay(1000).Wait();
            }
        }

        KeyValuePair<string, DateTime>? CheckAlarms()
        {
            foreach (var alarm in _config.Alarms)
            {
                if (DateTime.Now + TimeSpan.FromSeconds(_config.AlarmOffsetInSeconds) >= alarm.Value)
                {
                    return alarm;
                }
            }
            return null;
        }

        private AlarmPluginConfig _config = new();
    }

    [Serializable]
    public class AlarmPluginConfig
    {
        public Dictionary<string, DateTime> Alarms { get; set; } = new()
        {
            { "alarm1", DateTime.Parse("2024-04-17 07:11:00") },
            { "alarm2", DateTime.Parse("2024-04-17 12:11:00") }
        };
        public int AlarmOffsetInSeconds { get; set; } = 30;
    }
}
