using System.Reflection;

namespace avaness.PluginLoader.Data
{
    internal class ObsoletePlugin : PluginData
    {
        public new string Source => "Obsolete";
        public override bool IsLocal => false;
        public override bool IsCompiled => false;

        public override Assembly GetAssembly()
        {
            return null;
        }

        public override void Show()
        {

        }
    }
}