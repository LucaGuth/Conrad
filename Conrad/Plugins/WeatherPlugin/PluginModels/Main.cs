using System;

namespace WeatherPlugin.PluginModels;

[Serializable]
internal class Main
{
    public double Temp { get; set; }
    public double Feels_Like { get; set; }
    public int Humidity { get; set; }
}