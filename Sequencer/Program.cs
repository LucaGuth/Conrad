﻿using Sequencer;
using System;
using System.Reflection;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            string pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "");
            PluginLoader.LoadPluginsFromDirectory(pluginPath);

            var sequence = new Sequence(PluginLoader.GetNotifierPlugins(), PluginLoader.GetExecutorPlugins());

            sequence.Run();
        }
    }
}