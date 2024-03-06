using System;

namespace WeatherPlugin.PluginModels;

[Serializable]
internal class City
{
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public int Timezone { get; set; }
    public long Sunrise { get; set; }
    public long Sunset { get; set; }
}