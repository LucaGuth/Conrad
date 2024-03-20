namespace PluginInterfaces
{
    /// <summary>
    /// The interface for output plugins.
    /// </summary>
    public interface ILangaugeModel : IPlugin
    {
        /// <summary>
        /// The method that is called to process a promt.
        /// </summary>
        /// <param name="promt">The promt</param>
        /// <returns>The result form the Langauge Model</returns>
        string Process(string promt);
    }
}
