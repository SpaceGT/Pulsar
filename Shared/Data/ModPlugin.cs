using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using ProtoBuf;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data;

[ProtoContract]
public class ModPlugin : PluginData
{
    public override bool IsLocal => false;
    public override bool IsCompiled => false;

    [XmlIgnore]
    public ulong WorkshopId { get; private set; }

    public override string Id
    {
        get { return base.Id; }
        set
        {
            base.Id = value;
            WorkshopId = ulong.Parse(Id);
        }
    }

    public ModPlugin() { }

    public override Assembly GetAssembly()
    {
        return null;
    }

    public override bool TryLoadAssembly(out Assembly a)
    {
        a = null;
        return false;
    }

    private string modLocation;
    private bool isLegacy;
    public string ModLocation
    {
        get
        {
            if (modLocation is not null)
                return modLocation;
            modLocation = Path.Combine(
                Path.GetFullPath(ConfigManager.Instance.ModDir),
                WorkshopId.ToString()
            );
            if (
                Directory.Exists(modLocation)
                && !Directory.Exists(Path.Combine(modLocation, "Data"))
            )
            {
                string legacyFile = Directory
                    .EnumerateFiles(modLocation, "*_legacy.bin")
                    .FirstOrDefault();
                if (legacyFile is not null)
                {
                    isLegacy = true;
                    modLocation = legacyFile;
                }
            }
            return modLocation;
        }
    }

    public bool Exists => Directory.Exists(ModLocation) || (isLegacy && File.Exists(modLocation));

    public override void UpdateProfile(Profile draft, bool enabled)
    {
        base.UpdateProfile(draft, enabled);

        if (enabled)
            draft.Mods.Add(WorkshopId);
    }
}
