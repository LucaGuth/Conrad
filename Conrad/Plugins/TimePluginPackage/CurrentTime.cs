using PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimePluginPackage
{
    internal class CurrentTime : IPromptAdderPlugin
    {
        public string PromptAddOn
        {
            get
            {
                return $"The current time is: {DateTime.Now.ToString("yyyy-MM-dd HH:mm")}\n";
            }
        }

        public string Name => "Current Time";

        public string Description => "Gets the current Time and Date.";
    }
}
