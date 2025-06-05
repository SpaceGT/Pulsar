using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace avaness.PluginLoader.Config
{
    public class RemotePluginConfig
    {
        public string Name { get; set; }
        public string Repo { get; set; }
        public string Branch { get; set; }
        public string File { get; set; }
        public DateTime? LastCheck { get; set; }
        public bool Enabled { get; set; }
        public bool Trusted { get; set; }
    }
}
