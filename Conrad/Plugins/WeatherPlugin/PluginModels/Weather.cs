using System;

namespace WeatherPlugin.PluginModels;

[Serializable]
internal class Weather
{
    public string Main { get; set; } = "";
    public string Description { get; set; } = "";
}