using System;
using System.ComponentModel;
using System.Xml.Serialization;
using ProtoBuf;

namespace Pulsar.Shared.Assets;

[ProtoContract]
public sealed class PluginAsset
{
    public const string ReservedAssetFolder = "AssetFolder";

    [ProtoMember(1)]
    [XmlAttribute]
    public string Name { get; set; }

    [ProtoMember(2)]
    [XmlAttribute]
    public string Path { get; set; }

    [ProtoMember(3)]
    [XmlAttribute]
    public string Url { get; set; }

    [ProtoMember(4)]
    [XmlAttribute]
    public string Sha256 { get; set; }

    [ProtoMember(5)]
    [XmlAttribute]
    [DefaultValue(PluginAssetPlacement.Assets)]
    public PluginAssetPlacement Placement { get; set; }

    [ProtoMember(6)]
    [XmlAttribute]
    [DefaultValue(false)]
    public bool Extract { get; set; }

    [ProtoMember(7)]
    [XmlAttribute]
    [DefaultValue(false)]
    public bool Reference { get; set; }

    [ProtoMember(8)]
    [XmlAttribute]
    public string FileName { get; set; }

    [ProtoMember(9)]
    [XmlAttribute]
    public string Platforms { get; set; }

    [ProtoMember(10)]
    [XmlAttribute]
    public string Runtimes { get; set; }

    [XmlIgnore]
    public PluginAssetPlacement EffectivePlacement =>
        Reference ? PluginAssetPlacement.Bin : Placement;

    public bool IsSupportedEnvironment() => Tools.IsSupportedEnvironment(Runtimes, Platforms);

    internal string GetOutputFileName()
    {
        if (!string.IsNullOrWhiteSpace(FileName))
            return FileName;

        string path;
        if (!string.IsNullOrWhiteSpace(Path))
            path = Path;
        else
            path = new Uri(Url).LocalPath;

        return System.IO.Path.GetFileName(path);
    }
}

[ProtoContract]
public enum PluginAssetPlacement
{
    [ProtoEnum(Value = 0)]
    Assets = 0,

    [ProtoEnum(Value = 1)]
    Bin = 1,
}
