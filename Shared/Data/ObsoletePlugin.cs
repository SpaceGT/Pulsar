using System.Reflection;

namespace Pulsar.Shared.Data;

internal class ObsoletePlugin : PluginData
{
    public new string Source => "Obsolete";
    public override bool IsLocal => false;
    public override bool IsCompiled => false;

    public override Assembly GetAssembly()
    {
        return null;
    }
}
