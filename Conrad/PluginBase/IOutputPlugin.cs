using PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginBase
{
    /// <summary>
    /// The interface for output plugins.
    /// </summary>
    public interface IOutputPlugin : IPlugin
    {
        /// <summary>
        /// The method that is called to push a message to the output.
        /// </summary>
        /// <param name="message">The message that will be sent to the client.</param>
        void PushMessage(string message);
    }
}
