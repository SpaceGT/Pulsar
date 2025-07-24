using System.Xml.Serialization;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using ProtoBuf;

namespace Pulsar.Shared.Network
{
    [ProtoContract]
    public class NuGetPackageId
    {
        [ProtoMember(1)]
        [XmlElement]
        public string Name { get; set; }

        [ProtoIgnore]
        [XmlAttribute("Include")]
        public string NameAttribute
        {
            get => Name;
            set => Name = value;
        }

        [ProtoMember(2)]
        [XmlElement]
        public string Version { get; set; }

        [ProtoIgnore]
        [XmlAttribute("Version")]
        public string VersionAttribute
        {
            get => Version;
            set => Version = value;
        }

        public bool TryGetIdentity(out PackageIdentity id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Version))
                return false;

            if (!NuGetVersion.TryParse(Version, out NuGetVersion version))
                return false;

            id = new PackageIdentity(Name, version);
            return true;
        }
    }
}
