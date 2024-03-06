using System;
using System.Collections.Generic;

namespace WeatherPlugin.PluginModels;

[Serializable]
internal class WeatherForecast
{
    public List<Forecast> List { get; set; } = new();
    public City City { get; set; } = new();
}