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

    // Every file written to Bin and Assets, checked on load to catch interrupted writes
    [XmlArrayItem("File")]
    public CachedFile[] Files { get; set; }

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

        // Caches written before the file list existed are rebuilt once
        if (Files is null || !Files.All(IsIntact))
            return false;

        return Assets.All(asset => File.Exists(asset.Path) || Directory.Exists(asset.Path));
    }

    private bool IsIntact(CachedFile file)
    {
        FileInfo info = new(Path.Combine(RootDirectory, file.Path));
        if (info.Exists && info.Length == file.Size)
            return true;

        LogFile.Warn($"Cached file {info.FullName} is missing or incomplete, rebuilding");
        return false;
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
        Files = [.. GetFiles(BinDirectory).Concat(GetFiles(AssetsDirectory))];

        XmlSerializer serializer = new(typeof(PluginCache));
        using MemoryStream manifest = new();
        serializer.Serialize(manifest, this);
        Tools.WriteAllBytesAtomic(ManifestPath, manifest.ToArray());
    }

    private IEnumerable<CachedFile> GetFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => new CachedFile
            {
                Path = Tools.GetRelativePath(RootDirectory, file),
                Size = new FileInfo(file).Length,
            });
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

    public sealed class CachedFile
    {
        [XmlAttribute]
        public string Path { get; set; }

        [XmlAttribute]
        public long Size { get; set; }
    }
}
