using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace avaness.PluginLoader.Config
{
    public class LocalPluginConfig
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string File { get; set; }
        public bool Enabled { get; set; }
    }
}
