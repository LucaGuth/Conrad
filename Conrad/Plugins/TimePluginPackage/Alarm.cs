using PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TimePluginPackage
{
    internal class Alarm : INotifierPlugin, IExecutorPlugin, IConfigurablePlugin, IPromptAdderPlugin
    {
        public string Name => "AlarmClock";

        public string Description => "Sets and removes alarms.";

        public string ParameterFormat => @"Possible commands are
    setAlarm:'{alarmName}','{yyyy-MM-dd HH:mm}'
    setTimer:'{alarmName}','{mm:ss}'
    removeAlarm:'{alarmName}'
        'yyyy' is the year, 'MM' is the month, 'dd' is the day, 'HH' is the hour, 'mm' is the minute and 'ss' is the second. 'alarmName' is an identifier for the alarm.
";

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

        public event ConfigurationChangeEventHandler OnConfigurationChange;

        public event NotifyEventHandler OnNotify;

        public Task<string> ExecuteAsync(string parameter)
        {
            parameter = parameter.ToLower();
            var newAlarmRegex = new Regex(@"setalarm:'(.*)','(.*)'");
            var newTimerRegex = new Regex(@"settimer:'(.*)','(.*)'");
            var removeAlarmRegex = new Regex(@"removealarm:'(.*)'");

            bool configAltered = false;
            StringBuilder actions = new StringBuilder();

            if (newAlarmRegex.Match(parameter) is var newAlarmMatch && newAlarmMatch.Success)
            {
                var alarmName = newAlarmMatch.Groups[1].Value.Trim();
                var time = DateTime.Parse(newAlarmMatch.Groups[2].Value.Trim());
                _config.Alarms[alarmName] = time;
                actions.AppendLine($"New Alarm \"{alarmName}\" was set to {DateTime.Parse(newAlarmMatch.Groups[2].Value.Trim()).ToString("HH:mm, d. of MMMM yyyy")}");
                configAltered = true;
            }

            else if (newTimerRegex.Match(parameter) is var newTimerMatch && newTimerMatch.Success)
            {
                var time = DateTime.Now.Add(TimeSpan.ParseExact(newTimerMatch.Groups[2].Value.Trim(), "mm:ss", CultureInfo.InvariantCulture));
                _config.Alarms[newTimerMatch.Groups[1].Value.Trim()] = time;
                actions.AppendLine($"New Timer \"{newTimerMatch.Groups[1].Value.Trim()}\" will raise at {time.ToString("HH:mm")}");
                configAltered = true;
            }

            else if (removeAlarmRegex.Match(parameter) is var removeAlarmMatch && removeAlarmMatch.Success)
            {
                _config.Alarms.Remove(removeAlarmMatch.Groups[1].Value.Trim());
                configAltered = true;
            }

            if (configAltered)
            {
                OnConfigurationChange?.Invoke(this);
            }
            else
            {
                actions.AppendLine("Couldn't parse date, no alarm was set!");
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
        public Dictionary<string, DateTime> Alarms { get; set; } = [];
        public int AlarmOffsetInSeconds { get; set; } = -30;
    }
}
