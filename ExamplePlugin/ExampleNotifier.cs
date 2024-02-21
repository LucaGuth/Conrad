using PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExamplePluginPackage
{
    internal class ExampleNotifier : INotifierPlugin
    {
        public string Name => throw new NotImplementedException();
    }
}
