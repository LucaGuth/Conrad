using PluginInterfaces;
using System.Globalization;
using System.Runtime.Serialization;

namespace TimePluginPackage
{
    public class CurrentTime : IPromptAdderPlugin
    {
        public string PromptAddOn
        {
            get
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
        }

        public string Name => "CurrentTime";

        public string Description => "Adds the current time into the Prompt.";
    }
}
