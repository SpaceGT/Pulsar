using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using ProtoBuf;
using Pulsar.Compiler;
using Pulsar.Shared.Assets;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;
using Pulsar.Shared.Splash;

namespace Pulsar.Shared.Data;

[ProtoContract]
public class GitHubPlugin : PluginData
{
    public override bool IsLocal => false;
    public override bool IsCompiled => true;

    [ProtoMember(1)]
    public string Commit { get; set; }

    [ProtoMember(2)]
    [XmlArray]
    [XmlArrayItem("Directory")]
    public string[] SourceDirectories { get; set; }

    [ProtoMember(3)]
    [XmlArray]
    [XmlArrayItem("Version")]
    public GitHubSource[] AlternateVersions { get; set; }

    [ProtoMember(5)]
    public NuGetPackageList NuGetReferences { get; set; }

    private string _repoId;

    [ProtoMember(6)]
    public string RepoId
    {
        get => _repoId ?? Id;
        set => _repoId = value;
    }

    [ProtoMember(7)]
    [XmlElement("Asset")]
    public PluginAsset[] Assets { get; set; }

    private GitHubPluginConfig settings;
    private string cacheName;
    private PluginCache cache;

    public GitHubPlugin()
    {
        Status = PluginStatus.None;
    }

    public static void ClearGitHubCache()
    {
        string pluginCache = Path.Combine(ConfigManager.Instance.PulsarDir, "GitHub");
        if (!Directory.Exists(pluginCache))
            return;

        try
        {
            LogFile.WriteLine("Deleting plugin cache because of an update");
            Directory.Delete(pluginCache, true);
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to delete plugin cache: " + e);
        }
    }

    public override void LoadData(PluginDataConfig config)
    {
        if (config is GitHubPluginConfig githubConfig && IsValidConfig(githubConfig))
            settings = Tools.DeepCopy(githubConfig);
    }

    private bool IsValidConfig(GitHubPluginConfig githubConfig)
    {
        if (string.IsNullOrWhiteSpace(githubConfig.SelectedVersion))
            return true;
        if (AlternateVersions is null)
            return false;
        return AlternateVersions.Any(x =>
            x.Name.Equals(githubConfig.SelectedVersion, StringComparison.OrdinalIgnoreCase)
        );
    }

    public void InitPaths()
    {
        string friendlyName = Tools.CleanFileName(FriendlyName);
        cacheName = $"{friendlyName}-{Tools.GetStringHash(Id).Substring(0, 8)}";
        string cacheDirectory = Path.Combine(ConfigManager.Instance.PulsarDir, "GitHub", cacheName);
        cache = PluginCache.Load(cacheDirectory);
    }

    public override Assembly GetAssembly()
    {
        InitPaths();
        PluginAsset[] assets = Assets ?? [];

        Version gameVersion = ConfigManager.Instance.GameVersion;
        GitHubSource selectedVersion = GetSelectedVersion();
        string selectedRepo = selectedVersion?.Repo ?? RepoId;
        string selectedCommit = selectedVersion?.Commit ?? Commit;
        string fingerprint = GetFingerprint(selectedRepo, selectedCommit, assets);

        if (!cache.IsValid(fingerprint, gameVersion))
        {
            var lbl = SplashManager.Instance;
            lbl?.SetText($"Downloading '{FriendlyName}'");

            cache.Clear();
            string name = cacheName + '_' + Path.GetRandomFileName();
            Action<float> setBarValue = lbl is not null ? lbl.SetBarValue : null;
            AssetResolution resolution = CompileFromSource(
                selectedRepo,
                selectedCommit,
                name,
                assets,
                setBarValue
            );
            cache.SetAssets(resolution.Assets);
            cache.Save(fingerprint, gameVersion);

            Status = PluginStatus.Updated;
            lbl?.SetText($"Compiled '{FriendlyName}'");
        }
        namedAssets = cache.GetAssets();
        Assembly a = Assembly.LoadFrom(cache.DllFile);
        Version = a.GetName().Version;
        return a;
    }

    private GitHubSource GetSelectedVersion()
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.SelectedVersion))
            return null;
        return AlternateVersions?.FirstOrDefault(x =>
            x.Name.Equals(settings.SelectedVersion, StringComparison.OrdinalIgnoreCase)
        );
    }

    private AssetResolution CompileFromSource(
        string repo,
        string commit,
        string assemblyName,
        PluginAsset[] assets,
        Action<float> callback = null
    )
    {
        ICompiler compiler = Tools.Compiler.Create();
        AssetResolution resolution;
        using (Stream s = GitHub.GetRepoArchive(repo, commit))
        using (ZipArchive zip = new(s))
        {
            callback?.Invoke(0);
            for (int i = 0; i < zip.Entries.Count; i++)
            {
                ZipArchiveEntry entry = zip.Entries[i];
                CompileFromSource(compiler, entry);
                callback?.Invoke(i / (float)zip.Entries.Count);
            }

            if (NuGetReferences?.HasPackages == true)
            {
                NuGetRestoreResult restore = NuGetRestore.Run(NuGetReferences);

                foreach (string file in restore.CompileFiles)
                    compiler.TryAddDependency(file);

                foreach (NuGetRestoreFile file in restore.RuntimeFiles)
                {
                    string newFile = Path.Combine(cache.BinDirectory, file.OutputPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(newFile));
                    File.Copy(file.SourcePath, newFile);
                }
            }

            AssetResolver resolver = new(cache);
            resolution = resolver.Resolve(assets, archive: zip);
        }

        foreach (string reference in resolution.References)
            compiler.TryAddDependency(reference);
        callback?.Invoke(1);
        byte[] data = compiler.Compile(assemblyName, out _);
        Directory.CreateDirectory(cache.BinDirectory);
        File.WriteAllBytes(cache.DllFile, data);
        return resolution;
    }

    private void CompileFromSource(ICompiler compiler, ZipArchiveEntry entry)
    {
        string path = AssetResolver.RemoveArchiveRoot(entry.FullName);
        if (AllowedZipPath(path))
        {
            using Stream entryStream = entry.Open();
            compiler.Load(entryStream, path, embedFile: null);
        }
    }

    private bool AllowedZipPath(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;

        if (SourceDirectories is null || SourceDirectories.Length == 0)
            return true;

        foreach (string dir in SourceDirectories)
        {
            if (path.StartsWith(dir, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private string GetFingerprint(string repo, string commit, PluginAsset[] assets)
    {
        string context = string.Join(
            "\n",
            Id,
            repo,
            commit,
            string.Join(";", SourceDirectories ?? []),
            AssetResolver.GetDefinitionFingerprint(assets),
            NuGetReferences?.GetFingerprint()
        );
        return Tools.GetStringHash(context);
    }

    public override void UpdateProfile(Profile draft, bool enabled)
    {
        base.UpdateProfile(draft, enabled);

        if (enabled)
            draft.GitHub.Add(new() { Id = Id });
    }

    public override void InvalidateCache()
    {
        try
        {
            if (cache is null)
                InitPaths();
            cache.Invalidate();
            LogFile.WriteLine(
                $"Cache for GitHub plugin {RepoId} was invalidated, it will need to be compiled again at next game start"
            );
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to invalidate github cache: " + e);
        }
    }

    [ProtoContract]
    public class GitHubSource
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public string Commit { get; set; }

        [ProtoMember(3)]
        public string Repo { get; set; }

        public GitHubSource() { }
    }
}
