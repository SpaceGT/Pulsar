using System;
using System.Collections.Generic;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data
{
    public class Profile
    {
        public string Key => Tools.CleanFileName(Name);

        // Name of the profile
        public string Name { get; set; }

        // Plugin IDs
        public HashSet<string> Plugins { get; set; }

        public Profile() { }

        public Profile(string name, HashSet<string> plugins)
        {
            Name = name;
            Plugins = plugins;
        }

        public IEnumerable<PluginData> GetPlugins()
        {
            foreach (string id in Plugins)
            {
                if (ConfigManager.Instance.List.TryGetPlugin(id, out PluginData plugin))
                    yield return plugin;
            }
        }

        public string GetDescription()
        {
            int locals = 0;
            int plugins = 0;
            int mods = 0;
            foreach (PluginData plugin in GetPlugins())
            {
                if (plugin.IsLocal)
                    locals++;
                else if (plugin is ModPlugin)
                    mods++;
                else
                    plugins++;
            }

            List<string> infoItems = [];
            if (locals > 0)
                infoItems.Add(locals > 1 ? $"{locals} local plugins" : "1 local plugin");
            if (plugins > 0)
                infoItems.Add(plugins > 1 ? $"{plugins} plugins" : "1 plugin");
            if (mods > 0)
                infoItems.Add(mods > 1 ? $"{mods} mods" : "1 mod");

            return string.Join(", ", infoItems);
        }
    }
}
