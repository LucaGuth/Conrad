using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PluginInterfaces;
using System.Runtime.CompilerServices;
using System.Linq;

namespace TimePluginPackage
{
    internal class Alarm : INotifierPlugin, IExecutorPlugin, IConfigurablePlugin, IPromptAdderPlugin
    {
        public string Name => "AlarmClock";

        public string Description => "Sets and removes and raises alarms.";

        public string ParameterFormat => @"Action:'{setAlarm/removeAlarm}', AlarmName:'{name}', AlarmDescription:'{description}', Time:'{yyyy-MM-dd HH:mm:ss}', Daily:'{true/false}'";

        public string PromptAddOn
        {
            get
            {
                StringBuilder promptAdder = new();

                if (_config.Alarms.Any())
                {
                    foreach ((var alarmName, var alarmTime) in _config.Alarms)
                    {
                        promptAdder.AppendLine($"Alarm \"{alarmName}\" is set to {alarmTime.RaiseTime.ToString("HH:mm")} on {alarmTime.RaiseTime.ToString("yyyy-MM-dd")}");
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
            var nameRegex = new Regex(@".*alarmname:('.*?'),?");
            var timeRegex = new Regex(@".*time:('.*?'),?");
            var descriptionRegex = new Regex(@".*alarmdescription:('.*?'),?");
            var repeatRegex = new Regex(@"((?:true)|(?:false))");

            var action = actionRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);
            var alarmName = nameRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS);

            StringBuilder messages = new StringBuilder();

            try
            {
                switch (action.Trim())
                {
                    case "setalarm":
                        var alarm = new AlarmEntry();
                        alarm.RaiseTime = DateTime.Parse(timeRegex.Match(parameter).Groups[1].Value.Trim(TRIM_CHARS));

                        var descriptionMatch = descriptionRegex.Match(parameter);
                        var repeatMatch = repeatRegex.Match(parameter);

                        if (descriptionMatch.Success)
                        {
                            alarm.Description = descriptionMatch.Groups[1].Value.Trim(TRIM_CHARS);
                        }

                        if (repeatMatch.Success)
                        {
                            alarm.Daily = descriptionMatch.Groups[1].Value.Trim(TRIM_CHARS) == "true";
                        }

                        _config.Alarms[alarmName] = alarm;

                        var message = $"New Alarm \"{alarmName}\" was set to {alarm.RaiseTime.ToString("HH:mm, d.")} {alarm.RaiseTime.ToString("MMMM yyyy")}";
                        messages.AppendLine(message);
                        Log.Information("[AlarmClock]: Set Alarm {AlarmName} to {Time}", alarmName, alarm.RaiseTime.ToString());
                        OnConfigurationChange?.Invoke(this); break;
                    case "removealarm":
                        if (_config.Alarms.Remove(alarmName))
                        {
                            Log.Information("[AlarmClock]: Removed Alarm {AlarmName}", alarmName);
                            messages.AppendLine($"Removed Alarm \"{alarmName}\" from the list. Tell the user that the alarm was sucessfully removed.");
                            OnConfigurationChange?.Invoke(this);
                        }
                        else
                        {
                            Log.Information("[AlarmClock]: Tried to remove non-existing Alarm {AlarmName}", alarmName);
                            messages.AppendLine($"Tried to remove non-existing Alarm \"{alarmName}\"");
                        }
                        break;
                    default:
                        messages.AppendLine($"Couldn't run action: {action} because it does not exist!");
                        Log.Information("[AlarmClock]: Tried to run non-existing action {Action}", action);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "[AlarmClock]: Error while executing action {Action}", action);
                messages.AppendLine($"Error while executing action {action}");
            }

            return Task.FromResult(messages.ToString());
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
                    var finishedAlarmEntry = finishedAlarm.Value.Value;

                    if (finishedAlarmEntry.Daily)
                    {
                        _config.Alarms[finishedAlarmName].RaiseTime = finishedAlarmEntry.RaiseTime.AddDays(1);
                    }
                    else
                    {
                        _config.Alarms.Remove(finishedAlarm.Value.Key);
                    }
                    OnConfigurationChange?.Invoke(this);
                    OnNotify?.Invoke(this, $"The Alarm \"{finishedAlarmName}\" was raised. The time {finishedAlarmEntry.RaiseTime.ToString("HH:mm")} is over.\n{finishedAlarmEntry.Description}");
                }

                Task.Delay(1000).Wait();
            }
        }

        private KeyValuePair<string, AlarmEntry>? CheckAlarms()
        {
            try
            {
                 return _config.Alarms.Where(a => DateTime.Now.AddSeconds(-_config.AlarmOffsetInSeconds) >= a.Value.RaiseTime).First();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private AlarmPluginConfig _config = new();
    }

    [Serializable]
    public class AlarmPluginConfig
    {
        public Dictionary<string, AlarmEntry> Alarms { get; set; } = new()
        {
            {
                "InformationUseCase",
                new AlarmEntry()
                {
                    Daily = true,
                    Description = "What is the latest news? What will the weather be like where I live tomorrow and what do " +
                                  "you recommend I wear? Please give me stock information about my favourite stock company.",
                    RaiseTime = DateTime.Parse("20:00:00")
                }
            },
            {
                "EntertainmentUseCase",
                new AlarmEntry()
                {
                    Daily = true,
                    Description = "Please use the WikipediaPlugin to tell me something about an interesting topic in " +
                                  "the IT  sector and the TechnologyTrendsInformer to tell me about the latest " +
                                  "technology trend. Furthermore, use the QuizPlugin to provide three questions and " +
                                  "their answers. Please note that the quiz does not have to be related to the other " +
                                  "two topics.",
                    RaiseTime = DateTime.Parse("16:00:00")
                }
            },
            {
                "CulinaryDelightsUseCase",
                new AlarmEntry()
                {
                    Daily = true,
                    Description = "Please use the DishPlugin to tell me a recipe for a delicious dish in my favourite " +
                          "cuisine and the CoffeeBeverageProvider to tell me about a coffee beverage I can try." +
                          "Do you know any restaurants when I don't want to cook?",
                    RaiseTime = DateTime.Parse("12:00:00")
                }
            },
            {
                "MorningUseCase",
                new AlarmEntry()
                {
                    Daily = true,
                    Description = "Please use the WeatherPlugin to tell me about the weather today and the CalendarProvider " +
                          "to tell me about my appointments for today. Please use the DB Train Information to tell me how I get " +
                          "to work when I am at the railway station in one hour.",
                    RaiseTime = DateTime.Parse("16:00:00")
                }
            }

        };
        public int AlarmOffsetInSeconds { get; set; } = -30;
    }

    public class AlarmEntry
    {
        public DateTime RaiseTime { get; set; } = DateTime.MinValue;
        public string Description { get; set; } = string.Empty;
        public bool Daily { get; set; } = false;
    }
}
