using System.Text.RegularExpressions;

namespace BahnPlugin;

internal class ParameterModel
{
    internal ParameterModel(string parameter)
    {
        const string pattern = @"DepartureStation:'(?<departureStation>[^']*)', DestinationStation:'(?<destinationStation>[^']*)', DepartureTime:'(?<departureTime>\d{4}-\d{2}-\d{2} \d{2}:\d{2})'";
        var match = Regex.Match(parameter, pattern);

        if (!match.Success)
            throw new ArgumentException(
                "The invalid parameter format could not be parsed, please refer to the Bahn-Plugin description for help.");
        var departureStation = match.Groups["departureStation"].Value;
        var destinationStation = match.Groups["destinationStation"].Value;
        var departureTime = match.Groups["departureTime"].Value;

        DepartureStation = departureStation;
        DestinationStation = destinationStation;
        DepartureTime = departureTime;
    }
    
    public string DepartureStation { get; }
    public string DestinationStation { get; }
    public string DepartureTime { get; }
}
