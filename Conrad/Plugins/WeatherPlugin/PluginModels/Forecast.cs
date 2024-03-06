using System;
using System.Collections.Generic;

namespace WeatherPlugin.PluginModels;

[Serializable]
internal class Forecast
{
    public Main Main { get; set; } = new();
    public List<Weather> Weather { get; set; } = new();
    public Wind Wind { get; set; } = new();
    public int Visibility { get; set; }
    public string Dt_Txt { get; set; } = "";
}