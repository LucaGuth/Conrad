using System.Globalization;
using System.Text.RegularExpressions;
using Serilog;

namespace BahnPlugin;

internal class ParameterModel
{
    internal ParameterModel(string parameter)
    {
        const string patternFirstAttempt = @"(?:departure\s*(?:station)?)\s*:\s*'(?<departureStation>[^']+)'|" +
                               @"(?:destination\s*(?:station)?)\s*:\s*'(?<destinationStation>[^']+)'|" +
                               @"(?:(?:departure|time))\s*:\s*'(?<departureTime>[^']+)'";
        const string patternSecondAttempt = @"(?:\{([^\}]+)\})|(?:'([^']+)')";
        const string patternDateTime = @"\b(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\b";
        char[] charsToTrim = ['{', '}', '*', ',', '.', ' ', '\''];

        var (departureStation, destinationStation, departureTime)
            = AttemptFirstMatch(parameter, patternFirstAttempt, charsToTrim);

        if (string.IsNullOrEmpty(departureStation) && string.IsNullOrEmpty(destinationStation))
            (departureStation, destinationStation) = AttemptSecondMatch(parameter, patternSecondAttempt, charsToTrim);

        (departureStation, destinationStation) = ThrowIfNotValid(departureStation, destinationStation);

        if (string.IsNullOrEmpty(departureTime))
            departureTime = AttemptSecondMatchOrUseDefault(parameter, patternDateTime, charsToTrim);

        Log.Debug("[{PluginName}] Parsed parameters: DepartureStation: {DepartureStation}, DestinationStation: {DestinationStation}, DepartureTime: {DepartureTime}",
            nameof(BahnPlugin), departureStation, destinationStation, departureTime);

        DepartureStation = departureStation;
        DestinationStation = destinationStation;
        DepartureTime = departureTime;
    }

    public string DepartureStation { get; }
    public string DestinationStation { get; }
    public string DepartureTime { get; }

    private string AttemptSecondMatchOrUseDefault(string parameter, string patternDateTime, char[] charsToTrim)
    {
        // If no match, try finding a date-time-like string in the parameter
        var dateTimeRegex = new Regex(patternDateTime, RegexOptions.IgnoreCase);
        var matchDateTime = dateTimeRegex.Match(parameter);
        string? departureTime;

        if (matchDateTime.Success)
        {
            if (DateTime.TryParseExact(matchDateTime.Value.Trim(charsToTrim), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedDateTime))
            {
                departureTime = parsedDateTime.ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                Log.Warning("Found a datetime-like value, but it could not be parsed into a valid DateTime object.");
                departureTime = SetDefaultDepartureTime();
            }
        }
        else
        {
            Log.Warning("No datetime-like value was found in the input parameter.");
            departureTime = SetDefaultDepartureTime();
        }

        return departureTime;
    }

    private (string departureStation, string destinationStation) ThrowIfNotValid(string? departureStation, string? destinationStation)
    {
        if (string.IsNullOrEmpty(departureStation) && string.IsNullOrEmpty(destinationStation))
        {
            Log.Error("DepartureStation and DestinationStation could not be parsed from the input parameters.");
            throw new ArgumentException("DepartureStation and DestinationStation could not be parsed from the input parameter.");
        }

        if (string.IsNullOrEmpty(departureStation))
        {
            Log.Error("DepartureStation could not be parsed from the input parameter.");
            throw new ArgumentException("DepartureStation is missing or invalid.");
        }

        if (!string.IsNullOrEmpty(destinationStation)) return (departureStation, destinationStation);
        Log.Error("DestinationStation could not be parsed from the input parameter.");
        throw new ArgumentException("DestinationStation is missing or invalid.");
    }

    private (string? departureStation, string? destinationStation) AttemptSecondMatch(string parameter, string patternSecondAttempt, char[] charsToTrim)
    {
        var matchesSecond = Regex.Matches(parameter, patternSecondAttempt, RegexOptions.IgnoreCase);
        string? departureStation = null, destinationStation = null;
        if (2 <= matchesSecond.Count)
        {
            // Extract and format the first two stations found
            for (var i = 0; i < 2; i++)
            {
                var match = matchesSecond[i];
                var groupValue = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                switch (i)
                {
                    case 0:
                        // First match corresponds to the departure station
                        departureStation = FormatCityName(groupValue.Trim(charsToTrim));
                        break;
                    case 1:
                        // Second match corresponds to the destination station
                        destinationStation = FormatCityName(groupValue.Trim(charsToTrim));
                        break;
                }
            }
        }
        return (departureStation, destinationStation);
    }

    private (string? departureStation, string? destinationStation, string? departureTime) AttemptFirstMatch(string parameter, string patternFirstAttempt, char[] charsToTrim)
    {
        var matches = Regex.Matches(parameter, patternFirstAttempt, RegexOptions.IgnoreCase);
        string? departureStation = null, destinationStation = null, departureTime = null;

        foreach (Match match in matches)
        {
            if (match.Groups["departureStation"].Success)
            {
                departureStation = FormatCityName(match.Groups["departureStation"].Value.Trim(charsToTrim));
            }

            if (match.Groups["destinationStation"].Success)
            {
                destinationStation = FormatCityName(match.Groups["destinationStation"].Value.Trim(charsToTrim));
            }

            if (match.Groups["departureTime"].Success)
            {
                var parsedDateTime = match.Groups["departureTime"].Value;
                departureTime = DateTime.Parse(parsedDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None)
                    .ToString("yyyy-MM-dd HH:mm");
            }
        }
        return (departureStation, destinationStation, departureTime);
    }

    private string FormatCityName(string cityName)
    {
        // Replace 'Hauptbahnhof' with 'Hbf' if found in the city names
        cityName = cityName.Replace("Hauptbahnhof", "Hbf");

        // Regex pattern to find common prepositions in city names and capture the preposition and following part
        var pattern = new Regex(@"\s+(am|an der)\s+(.*)");
        var match = pattern.Match(cityName);

        return match.Success ?
            // Reformat the city name by placing the captured part in parentheses
            $"{cityName[..match.Index]} ({match.Groups[2].Value})" :
            // Return the original name if no common preposition is found
            cityName;
    }

    // Define a method to set departureTime with a default value, encapsulating repeated logic
    private string SetDefaultDepartureTime()
    {
        Log.Warning("Setting default departure time. {DefaultTime} will be processed as default.",
            DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"));
        return DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");
    }
}
