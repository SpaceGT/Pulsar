using System.Linq;
using System.Xml.Serialization;
using ProtoBuf;

namespace Pulsar.Shared.Network;

[ProtoContract]
public class NuGetPackageList
{
    [ProtoMember(2)]
    [XmlElement("PackageReference")]
    public NuGetPackageId[] PackageIds { get; set; }

    public bool HasPackages => PackageIds?.Any(x => x.TryGetIdentity(out _)) == true;

    public string GetFingerprint()
    {
        if (!HasPackages)
            return null;

        var packages = PackageIds
            .Select(x => x.TryGetIdentity(out var id) ? id : null)
            .Where(x => x is not null)
            .OrderBy(x => x.Id, System.StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Id}@{x.Version}");

        return Tools.GetStringHash(string.Join("\n", packages));
    }
}
