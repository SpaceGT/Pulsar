using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Pulsar.Shared.Data;

[XmlRoot("CacheManifest")]
public sealed class PluginCache
{
    internal const string PluginFile = "plugin.dll";
    private const string ManifestFile = "manifest.xml";
    private const string AssetsFolder = "Assets";
    private const string BinFolder = "Bin";

    [XmlIgnore]
    public string RootDirectory { get; private set; }

    [XmlIgnore]
    public string AssetsDirectory => Path.Combine(RootDirectory, AssetsFolder);

    [XmlIgnore]
    public string BinDirectory => Path.Combine(RootDirectory, BinFolder);

    [XmlIgnore]
    public string DllFile => Path.Combine(BinDirectory, PluginFile);

    private string ManifestPath => Path.Combine(RootDirectory, ManifestFile);

    public string Fingerprint { get; set; }

    public string Runtime { get; set; }

    public string RuntimeIdentifier { get; set; }

    public string PulsarVersion { get; set; }

    public string GameVersion { get; set; }

    [XmlArrayItem("Asset")]
    public CachedAsset[] Assets { get; set; } = [];

    public static PluginCache Load(string rootDirectory)
    {
        rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(rootDirectory);

        PluginCache cache = new();
        string manifest = Path.Combine(rootDirectory, ManifestFile);

        if (File.Exists(manifest))
        {
            XmlSerializer serializer = new(typeof(PluginCache));
            try
            {
                using Stream file = File.OpenRead(manifest);
                cache = (PluginCache)serializer.Deserialize(file);
            }
            catch (Exception e)
            {
                LogFile.WriteLine("Error while loading plugin cache: " + e);
                cache = new();
            }
        }

        cache.RootDirectory = rootDirectory;
        return cache;
    }

    public bool IsValid(string fingerprint, Version gameVersion)
    {
        Version pulsarVersion = Assembly.GetEntryAssembly().GetName().Version;

        if (
            !File.Exists(DllFile)
            || Fingerprint != fingerprint
            || Runtime != RuntimeInformation.FrameworkDescription
            || RuntimeIdentifier != Tools.RuntimeIdentifier
            || PulsarVersion != pulsarVersion.ToString()
        )
            return false;

        if (gameVersion is not null && GameVersion != gameVersion.ToString())
            return false;

        return Assets.All(asset => File.Exists(asset.Path) || Directory.Exists(asset.Path));
    }

    public void Clear()
    {
        Invalidate();
        Assets = [];

        if (Directory.Exists(AssetsDirectory))
            Directory.Delete(AssetsDirectory, true);

        if (Directory.Exists(BinDirectory))
            Directory.Delete(BinDirectory, true);
    }

    public void Save(string fingerprint, Version gameVersion)
    {
        Version pulsarVersion = Assembly.GetEntryAssembly().GetName().Version;

        Fingerprint = fingerprint;
        GameVersion = gameVersion?.ToString();
        Runtime = RuntimeInformation.FrameworkDescription;
        RuntimeIdentifier = Tools.RuntimeIdentifier;
        PulsarVersion = pulsarVersion.ToString();

        XmlSerializer serializer = new(typeof(PluginCache));
        using Stream file = File.Create(ManifestPath);
        serializer.Serialize(file, this);
    }

    public void SetAssets(IReadOnlyDictionary<string, string> assets)
    {
        Assets = [.. assets.Select(item => new CachedAsset { Name = item.Key, Path = item.Value })];
    }

    public IReadOnlyDictionary<string, string> GetAssets() =>
        Assets.ToDictionary(
            asset => asset.Name,
            asset => asset.Path,
            StringComparer.OrdinalIgnoreCase
        );

    public void Invalidate()
    {
        Fingerprint = null;
        File.Delete(ManifestPath);
    }

    public sealed class CachedAsset
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
