using System.Text.RegularExpressions;
using Serilog;

namespace BahnPlugin;

internal class ParameterModel
{
    internal ParameterModel(string parameter)
    {
        const string pattern = @"DepartureStation:'(?<departureStation>[^']*)', DestinationStation:'(?<destinationStation>[^']*)', DepartureTime:'(?<departureTime>\d{4}-\d{2}-\d{2} \d{2}:\d{2})'";
        var match = Regex.Match(parameter, pattern);

        if (!match.Success)
        {
            Log.Error("The input parameter string does not match the expected format.");
            throw new ArgumentException("Invalid parameter format. Please refer to the Bahn-Plugin description for help.");
        }

        // Check each group individually for a more detailed error message
        var departureStation = match.Groups["departureStation"].Success ? match.Groups["departureStation"].Value : null;
        var destinationStation = match.Groups["destinationStation"].Success ? match.Groups["destinationStation"].Value : null;
        var departureTime = match.Groups["departureTime"].Success ? match.Groups["departureTime"].Value : null;

        if (string.IsNullOrEmpty(departureStation))
        {
            Log.Error("DepartureStation could not be parsed from the input parameters.");
            throw new ArgumentException("DepartureStation is missing or invalid.");
        }

        if (string.IsNullOrEmpty(destinationStation))
        {
            Log.Error("DestinationStation could not be parsed from the input parameters.");
            throw new ArgumentException("DestinationStation is missing or invalid.");
        }

        if (string.IsNullOrEmpty(departureTime))
        {
            Log.Error("DepartureTime could not be parsed from the input parameters.");
            throw new ArgumentException("DepartureTime is missing or invalid.");
        }

        DepartureStation = departureStation;
        DestinationStation = destinationStation;
        DepartureTime = departureTime;
    }

    
    public string DepartureStation { get; }
    public string DestinationStation { get; }
    public string DepartureTime { get; }
}
