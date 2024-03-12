using System.Globalization;
using System.Text.RegularExpressions;

namespace WeatherPlugin.PluginModels;

internal class ParameterModel
{
    internal ParameterModel(string parameter)
    {
        const string pattern = @"ForecastFromDate:'(?<fromDate>\d{4}-\d{2}-\d{2})', ForecastUntilDate:'(?<untilDate>\d{4}-\d{2}-\d{2})', ForecastCity:'(?<city>[^']*)'";
        var match = Regex.Match(parameter, pattern);

        if (!match.Success)
            throw new ArgumentException(
                "The invalid parameter format could not be parsed, please refer to the weather forecast description for help.");
        var fromDateStr = match.Groups["fromDate"].Value;
        var untilDateStr = match.Groups["untilDate"].Value;
        var city = match.Groups["city"].Value;

        if (DateTime.TryParseExact(fromDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var fromDate) &&
            DateTime.TryParseExact(untilDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
        {
            var untilDate = DateTime.Parse($"{untilDateStr} 23:59:59");

            if (fromDate > untilDate)
                throw new ArgumentException(
                    "The dates provided are not valid to request the weather forecast.");

            ForecastFromDate = fromDate;
            ForecastUntilDate = untilDate;
            City = city;
        }
        else
        {
            throw new ArgumentException(
                "Invalid date format, please refer to the weather forecast description for help.");
        }
    }
    
    public DateTime ForecastFromDate { get; set; }
    public DateTime ForecastUntilDate { get; set; }
    public string City { get; set; }
}