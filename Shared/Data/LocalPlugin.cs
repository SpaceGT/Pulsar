using System.IO;
using System.Reflection;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data
{
    public class LocalPlugin : PluginData
    {
        public override bool IsLocal => true;
        public override bool IsCompiled => false;

        public string Dll;

        private AssemblyResolver resolver;

        private LocalPlugin() { }

        public LocalPlugin(string dll)
        {
            Dll = dll;
            Id = Path.GetFileName(dll);
            FriendlyName = Path.GetFileNameWithoutExtension(dll);
            Status = PluginStatus.None;
        }

        public override Assembly GetAssembly()
        {
            if (File.Exists(Dll))
            {
                resolver = new AssemblyResolver();
                resolver.AddSourceFolder(Path.GetDirectoryName(Dll));
                resolver.AddAllowedAssemblyFile(Dll);
                Assembly a = Assembly.LoadFile(Dll);
                Version = a.GetName().Version;
                return a;
            }
            return null;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
