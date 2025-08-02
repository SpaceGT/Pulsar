using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using ProtoBuf;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data
{
    public interface ISteamItem
    {
        string Id { get; }
        ulong WorkshopId { get; }
    }

    [ProtoContract]
    public class ModPlugin : PluginData, ISteamItem
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

        [ProtoMember(1)]
        [XmlArray]
        [XmlArrayItem("Id")]
        public ulong[] DependencyIds { get; set; } = [];

        [XmlIgnore]
        public ModPlugin[] Dependencies { get; set; } = [];

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
                if (modLocation != null)
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
                    if (legacyFile != null)
                    {
                        isLegacy = true;
                        modLocation = legacyFile;
                    }
                }
                return modLocation;
            }
        }

        public bool Exists =>
            Directory.Exists(ModLocation) || (isLegacy && File.Exists(modLocation));

        public override bool UpdateEnabledPlugins(HashSet<string> enabledPlugins, bool enable)
        {
            bool changed = base.UpdateEnabledPlugins(enabledPlugins, enable);

            if (enable)
            {
                foreach (ModPlugin other in Dependencies)
                {
                    if (enabledPlugins.Add(other.Id))
                        changed = true;
                }
            }

            return changed;
        }
    }
}
