using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Pulsar.Shared.Assets;

internal sealed class AssetResolver(PluginCache cache)
{
    public static string GetDefinitionFingerprint(IEnumerable<PluginAsset> assets) =>
        Tools.GetStringHash(JsonConvert.SerializeObject(SupportedAssets(assets)));

    public static string GetDevfolderFingerprint(IEnumerable<PluginAsset> assets, string anchor)
    {
        PluginAsset[] declarations = [.. SupportedAssets(assets)];
        StringBuilder text = new(GetDefinitionFingerprint(declarations));

        foreach (PluginAsset asset in declarations)
        {
            bool usesCache = asset.Extract || asset.EffectivePlacement == PluginAssetPlacement.Bin;
            if (string.IsNullOrWhiteSpace(asset.Path) || !usesCache)
                continue;

            string path = ResolvePath(asset, anchor);
            if (File.Exists(path))
            {
                text.Append(Tools.GetFileHash(path)).Append('\n');
                continue;
            }

            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
            {
                string relativePath = Tools.GetRelativePath(path, file);
                string hash = Tools.GetFileHash(file);
                text.Append(relativePath).Append(':').Append(hash).Append('\n');
            }
        }

        return Tools.GetStringHash(text.ToString());
    }

    public static IReadOnlyDictionary<string, string> ResolveLocal(
        IEnumerable<PluginAsset> assets,
        string anchor
    )
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (PluginAsset asset in SupportedAssets(assets))
            result.Add(asset.Name, ResolvePath(asset, anchor));
        return result;
    }

    private static IEnumerable<PluginAsset> SupportedAssets(IEnumerable<PluginAsset> assets) =>
        (assets ?? []).Where(asset => asset.IsSupportedEnvironment());

    private static string ResolvePath(PluginAsset asset, string anchor) =>
        Path.GetFullPath(Path.Combine(anchor, asset.Path));

    public AssetResolution Resolve(
        IEnumerable<PluginAsset> assets,
        string anchor = null,
        ZipArchive archive = null
    )
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        List<string> references = [];

        foreach (PluginAsset asset in SupportedAssets(assets))
        {
            string destination;
            if (!string.IsNullOrWhiteSpace(asset.Url))
                destination = ResolveUrlAsset(asset);
            else if (archive is not null)
                destination = ResolveGithubAsset(asset, archive.Entries);
            else
                destination = ResolveDevfolderAsset(asset, anchor);

            result.Add(asset.Name, destination);
            if (asset.Reference)
                AddReferences(destination, references);
        }

        return new AssetResolution(result, references);
    }

    private string ResolveUrlAsset(PluginAsset asset)
    {
        if (asset.Extract)
            return ExtractTemporaryArchive(asset, file => Download(asset, file));

        string destination = GetDestination(asset, isDirectory: false);
        Download(asset, destination);
        return destination;
    }

    private string ResolveDevfolderAsset(PluginAsset asset, string anchor)
    {
        string source = ResolvePath(asset, anchor);
        if (!asset.Extract && asset.EffectivePlacement == PluginAssetPlacement.Assets)
            return source;

        if (asset.Extract)
        {
            string destination = GetDestination(asset, isDirectory: true);
            ExtractArchive(source, destination);
            return destination;
        }

        if (Directory.Exists(source))
        {
            string destination = GetDestination(asset, isDirectory: true);
            CopyDirectory(source, destination);
            return destination;
        }

        string file = GetDestination(asset, isDirectory: false);
        CopyFile(source, file);
        return file;
    }

    private static void AddReferences(string destination, List<string> references)
    {
        IEnumerable<string> files = Directory.Exists(destination)
            ? Directory.EnumerateFiles(destination, "*", SearchOption.TopDirectoryOnly)
            : [destination];
        references.AddRange(files.Where(Tools.IsManagedDll));
    }

    private string GetDestination(PluginAsset asset, bool isDirectory)
    {
        if (asset.EffectivePlacement == PluginAssetPlacement.Assets)
            return Path.Combine(cache.AssetsDirectory, asset.Name);

        return isDirectory
            ? cache.BinDirectory
            : Path.Combine(cache.BinDirectory, asset.GetOutputFileName());
    }

    private string ResolveGithubAsset(PluginAsset asset, IEnumerable<ZipArchiveEntry> entries)
    {
        string sourcePath = asset.Path.Trim('/');
        var files = entries.Where(entry => entry.Name.Length != 0); // Ignore directory entries

        ZipArchiveEntry file = files.FirstOrDefault(entry =>
            RemoveArchiveRoot(entry.FullName) == sourcePath
        );

        if (file is null)
            return ResolveGithubFolder(asset, sourcePath, files);

        if (asset.Extract)
            return ExtractTemporaryArchive(
                asset,
                destination => CopyGithubFile(asset, file, destination)
            );

        string destination = GetDestination(asset, isDirectory: false);
        CopyGithubFile(asset, file, destination);
        return destination;
    }

    private string ResolveGithubFolder(
        PluginAsset asset,
        string sourcePath,
        IEnumerable<ZipArchiveEntry> files
    )
    {
        string prefix = sourcePath + "/";
        IEnumerable<ZipArchiveEntry> children = files.Where(entry =>
        {
            string path = RemoveArchiveRoot(entry.FullName);
            return path.StartsWith(prefix, StringComparison.Ordinal);
        });

        if (!children.Any())
            throw new FileNotFoundException($"Repository asset '{asset.Name}' was not found.");

        string directory = GetDestination(asset, isDirectory: true);
        foreach (ZipArchiveEntry entry in children)
        {
            string path = RemoveArchiveRoot(entry.FullName);
            string relativePath = path.Substring(prefix.Length);
            CopyEntry(entry, Path.Combine(directory, relativePath));
        }

        return directory;
    }

    private static void CopyGithubFile(PluginAsset asset, ZipArchiveEntry entry, string destination)
    {
        CopyEntry(entry, destination);
        VerifyHash(asset, destination);
    }

    internal static string RemoveArchiveRoot(string path)
    {
        int separator = path.IndexOf('/');
        return separator < 0 ? path : path.Substring(separator + 1);
    }

    private static void CopyEntry(ZipArchiveEntry entry, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        entry.ExtractToFile(destination);
    }

    private static void VerifyHash(PluginAsset asset, string file)
    {
        if (string.IsNullOrWhiteSpace(asset.Sha256))
        {
            LogFile.Warn($"Asset '{asset.Name}' was loaded without a SHA-256 hash.");
            return;
        }

        string hash = Tools.GetFileHash(file);
        if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Asset '{asset.Name}' did not match its Sha256!");
    }

    private void Download(PluginAsset asset, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        Uri url = new(asset.Url, UriKind.Absolute);
        NetworkClient.DownloadAsync(url, destination).GetAwaiter().GetResult();
        VerifyHash(asset, destination);
    }

    private string ExtractTemporaryArchive(PluginAsset asset, Action<string> createArchive)
    {
        string archiveFile = Path.Combine(cache.RootDirectory, asset.GetOutputFileName());
        File.Delete(archiveFile);

        try
        {
            createArchive(archiveFile);
            string destination = GetDestination(asset, isDirectory: true);
            ExtractArchive(archiveFile, destination);
            return destination;
        }
        finally
        {
            File.Delete(archiveFile);
        }
    }

    private static void ExtractArchive(string archiveFile, string destination)
    {
        Directory.CreateDirectory(destination);
        ReaderOptions options = ReaderOptions.ForFilePath;
        ArchiveInformation information = ArchiveFactory.GetArchiveInformation(archiveFile, options);

        if (information?.SupportsRandomAccess == true)
        {
            using IArchive archive = ArchiveFactory.OpenArchive(archiveFile, options);
            archive.WriteToDirectory(destination);
            return;
        }

        using IReader reader = ReaderFactory.OpenReader(archiveFile, options);
        reader.WriteAllToDirectory(destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = Tools.GetRelativePath(source, file);
            CopyFile(file, Path.Combine(destination, relativePath));
        }
    }

    private static void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination);
    }
}

internal sealed class AssetResolution(
    IReadOnlyDictionary<string, string> assets,
    IReadOnlyList<string> references
)
{
    public IReadOnlyDictionary<string, string> Assets { get; } = assets;
    public IReadOnlyList<string> References { get; } = references;
}
