using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using PluginInterfaces;
using Serilog;

namespace CalendarPlugin;

public class CalendarPlugin : IExecutorPlugin, IConfigurablePlugin
{
    public string Name => "Calendar Provider";
    public string Description => "This plugin returns the events of a day.";
    public string ParameterFormat => "Day:'{YYYY-MM-DD}'\n\tThe parameter must be a string in the format 'YYYY-MM-DD'." +
                                     " A valid example would be '2024-04-02' for the 2nd of April 2024. If the" +
                                     " parameter is not provided or invalid, the plugin will return the events of the" +
                                     " current day.";

    public async Task<string> ExecuteAsync(string parameter)
    {
        Log.Debug("Starting execution of Calendar Plugin.");
        try
        {
            var date = ParseParameterToDate(parameter);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var jsonCredentials = JsonSerializer.Serialize(_config.ServiceAccount, options);

            var credential = GoogleCredential.FromJson(jsonCredentials)
                .CreateScoped(CalendarService.Scope.Calendar);

            // Create Google Calendar API service
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = _config.ApplicationName,
            });

            // Define the request
            var request = GetConfiguredRequest(service, date, _config.CalendarId);

            // Retrieve the events
            var events = await request.ExecuteAsync();

            // List the upcoming events
            return ListUpcomingEvents(events);
        }
        catch (Exception e)
        {
            Log.Error("An error occurred:\n{Source}\n{Error}", e.Source, e.Message);

            return e is NotSupportedException ? "An error occurred probably due to an authentication issue. " +
                                                "The event list could not be retrieved."
                : "An error occurred. The event list could not be retrieved.";
        }

    }

    private static EventsResource.ListRequest GetConfiguredRequest(CalendarService service, DateTime date, string calendarId)
    {
        // Define the start and end times as DateTimeOffset
        DateTimeOffset startTime;
        try
        {
            // Try to create a DateTimeOffset from the date
            startTime = new DateTimeOffset(date.Date, TimeSpan.Zero);
        }
        // Occurs when date equals DateTime.Today because the time is 00:00:00 and the offset is not valid (I think)
        // This happens, when the initial date parameter is DateTime.Today or not valid
        catch (ArgumentException)
        {
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(date); // Gets the local system's offset from UTC
            startTime = new DateTimeOffset(date, localOffset);
        }
        var endTime = startTime.AddDays(1);

        // Configure the request
        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = startTime;
        request.TimeMaxDateTimeOffset = endTime;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        return request;
    }

    private static string ListUpcomingEvents(Events events)
    {
        var result = new List<string>();
        if (events.Items is { Count: > 0 })
        {
            for (var index = 0; index < events.Items.Count; index++)
            {
                var eventItem = events.Items[index];
                string start, duration;
                TimeSpan? eventDuration = null;

                if (!string.IsNullOrEmpty(eventItem.Start.DateTimeRaw) &&
                    !string.IsNullOrEmpty(eventItem.End.DateTimeRaw))
                {
                    // Parse the raw date/time strings for start and end
                    var startDateTime = DateTimeOffset.Parse(eventItem.Start.DateTimeRaw);
                    var endDateTime = DateTimeOffset.Parse(eventItem.End.DateTimeRaw);
                    start = startDateTime.ToString("yyyy-MM-dd HH:mm");
                    eventDuration = endDateTime - startDateTime;
                }
                else if (eventItem.Start.Date == DateTime.Today.Date.ToString(CultureInfo.InvariantCulture))
                {
                    // Use the date as is if DateTimeRaw is not available (all-day events on the current day)
                    start = eventItem.Start.Date;
                }
                else
                {
                    // Skip the event since it's date is not today
                    continue;
                }

                // Format duration as a string (e.g., "2 hours 30 minutes")
                if (eventDuration.HasValue)
                {
                    var totalMinutes = (int)eventDuration.Value.TotalMinutes;
                    var hours = totalMinutes / 60;
                    var minutes = totalMinutes % 60;
                    duration =
                        $"{(hours > 0 ? $"{hours} hour{(hours > 1 ? "s" : "")}" : "")}" +
                        $"{(minutes > 0 ? $" {minutes} minute{(minutes > 1 ? "s" : "")}" : "")}"
                            .Trim();
                }
                else
                {
                    duration = "All day event";
                }

                // Retrieve the location
                var location = !string.IsNullOrEmpty(eventItem.Location)
                    ? $"at {eventItem.Location}"
                    : "No location specified";

                result.Add($"Event {index + 1}: {eventItem.Summary}, Start at: {start}, Duration: {duration}, " +
                              $"Location: {location}");
            }
        }
        else
        {
            result.Add("No events for the day.");
        }

        return string.Join("\n", result);
    }

    private static DateTime ParseParameterToDate(string parameter)
    {
        // Attempt to extract the date part from the parameter string using regex
        var match = Regex.Match(parameter, @"Day:'(\d{4}-\d{2}-\d{2})'");
        if (match.Success)
        {
            // If a date is successfully extracted, try to parse it to DateTime
            if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }
        }

        // If the parameter is not provided or invalid log a warning and return the current day
        Log.Warning("Invalid parameter provided. Using the current day.");
        return DateTime.Today;
    }

    private CalendarPluginConfig _config = new();

    public JsonNode GetConfigiguration()
    {
        var localConfig = JsonSerializer.Serialize(_config);
        var jsonNode = JsonNode.Parse(localConfig)!;

        return jsonNode;
    }

    public void LoadConfiguration(JsonNode configuration)
    {
        _config = configuration.Deserialize<CalendarPluginConfig>() ?? throw new InvalidDataException("The " +
            "config could not be loaded.");
    }

    public event ConfigurationChangeEventHandler? OnConfigurationChange;
}

[Serializable]
internal class CalendarPluginConfig
{
    public ServiceAccount ServiceAccount { get; set; } = new();
    public string CalendarId { get; set; } = "";
    public string ApplicationName { get; set; } = "Conrad";
}
