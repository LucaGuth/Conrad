namespace PluginInterfaces;

/// <summary>
/// The interface for plugins that are able to provide additional information for the prompt.
/// </summary>
public interface IPromptAdderPlugin : IPlugin
{
    /// <summary>
    /// The method that is called once the plugin needs to provide the additional prompt information.
    /// </summary>
    /// <returns>The additional information for the prompt.</returns>
    public string PromptAddOn { get; }
}
