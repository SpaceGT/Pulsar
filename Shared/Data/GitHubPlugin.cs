using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using ProtoBuf;
using Pulsar.Compiler;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;
using Pulsar.Shared.Splash;

namespace Pulsar.Shared.Data;

[ProtoContract]
public partial class GitHubPlugin : PluginData
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

    [ProtoMember(4)]
    public string AssetFolder { get; set; }

    [ProtoMember(5)]
    public NuGetPackageList NuGetReferences { get; set; }

    private string _repoId;

    [ProtoMember(6)]
    public string RepoId
    {
        get => _repoId ?? Id;
        set => _repoId = value;
    }

    private GitHubPluginConfig settings;
    private string assemblyName;
    private CacheManifest manifest;
    private NuGetClient nuget;
    private AssemblyResolver resolver;

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
        string[] nameArgs = RepoId.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (nameArgs.Length < 2)
            throw new Exception("Invalid GitHub name: " + RepoId);

        CleanPaths(SourceDirectories);

        if (!string.IsNullOrWhiteSpace(AssetFolder))
        {
            AssetFolder = AssetFolder.Replace('\\', '/').TrimStart('/');
            if (AssetFolder.Length > 0 && AssetFolder[AssetFolder.Length - 1] != '/')
                AssetFolder += '/';
        }

        assemblyName = MakeSafeString(nameArgs[1]);
        manifest = CacheManifest.Load(nameArgs[0], nameArgs[1]);
    }

    private void CleanPaths(string[] paths)
    {
        if (paths is not null)
        {
            for (int i = paths.Length - 1; i >= 0; i--)
            {
                string path = paths[i].Replace('\\', '/').TrimStart('/');

                if (path.Length == 0)
                    continue;

                if (path[path.Length - 1] != '/')
                    path += '/';

                paths[i] = path;
            }
        }
    }

    private string MakeSafeString(string s)
    {
        StringBuilder sb = new();
        foreach (char ch in s)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }

    public override Assembly GetAssembly()
    {
        InitPaths();

        Assembly a;

        resolver = new AssemblyResolver();

        Version gameVersion = ConfigManager.Instance.GameVersion;
        GitHubSource selectedVersion = GetSelectedVersion();
        string selectedRepo = selectedVersion?.Repo ?? RepoId;
        string selectedCommit = selectedVersion?.Commit ?? Commit;

        if (
            !manifest.IsCacheValid(
                selectedCommit,
                gameVersion,
                !string.IsNullOrWhiteSpace(AssetFolder),
                NuGetReferences is not null && NuGetReferences.HasPackages
            )
        )
        {
            var lbl = SplashManager.Instance;
            lbl?.SetText($"Downloading '{FriendlyName}'");

            manifest.GameVersion = gameVersion;
            manifest.Commit = selectedCommit;
            manifest.Runtime = RuntimeInformation.FrameworkDescription;
            manifest.ClearAssets();
            string name = assemblyName + '_' + Path.GetRandomFileName();
            Action<float> setBarValue = lbl is not null ? lbl.SetBarValue : null;
            byte[] data = CompileFromSource(selectedRepo, selectedCommit, name, setBarValue);
            File.WriteAllBytes(manifest.DllFile, data);
            manifest.DeleteUnknownFiles();
            manifest.Save();

            Status = PluginStatus.Updated;
            lbl?.SetText($"Compiled '{FriendlyName}'");
            resolver.AddSourceFolder(manifest.LibDir);
            resolver.AddAllowedAssemblyFile(manifest.DllFile);
            resolver.AddAllowedAssemblyName(name);
            a = Assembly.Load(data);
        }
        else
        {
            manifest.DeleteUnknownFiles();
            resolver.AddSourceFolder(manifest.LibDir);
            resolver.AddAllowedAssemblyFile(manifest.DllFile);
            a = Assembly.LoadFile(manifest.DllFile);
        }

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

    private byte[] CompileFromSource(
        string repo,
        string commit,
        string assemblyName,
        Action<float> callback = null
    )
    {
        ICompiler compiler = Tools.Compiler.Create();
    
        Tools.ShallowCloneRecurseSubmodules(manifest.SrcDir, $"https://github.com/{repo}", commit);
        callback?.Invoke(0);
    
        string[] repoFiles = Directory.GetFiles(manifest.SrcDir, "*.*", SearchOption.AllDirectories);
        for (int i = 0; i < repoFiles.Length; i++)
        {
            string normalizedFile = repoFiles[i].Replace('\\', '/');
            if (!normalizedFile.Contains("/.git/"))
            {
                CompileFromSource(compiler, normalizedFile);
                callback?.Invoke(i / (float)repoFiles.Length);
            }
        }
        if (NuGetReferences?.PackageIds is not null)
        {
            nuget ??= new NuGetClient();
            InstallPackages(nuget.DownloadPackages(NuGetReferences.PackageIds), compiler);
        }
        callback?.Invoke(1);
        return compiler.Compile(assemblyName, out _);
    }

    private void CompileFromSource(ICompiler compiler, string filePath)
    {
        string path = RemoveRoot(filePath);
        if (NuGetReferences is not null && path == NuGetReferences.PackagesConfigNormalized)
        {
            nuget = new NuGetClient();
            NuGetPackage[] packages;
            using (Stream entryStream = File.OpenRead(filePath))
            {
                packages = nuget.DownloadFromConfig(entryStream);
            }
            InstallPackages(packages, compiler);
        }
        if (AllowedZipPath(path))
        {
            using Stream entryStream = File.OpenRead(filePath);
            string relFile = path.Replace('/', '\\');
            compiler.Load(entryStream, relFile, embedFile: null);
        }
        if (IsAssetZipPath(path, out string assetFilePath))
        {
            AssetFile newFile = manifest.CreateAsset(assetFilePath);
            if (!manifest.IsAssetValid(newFile))
            {
                using Stream entryStream = File.OpenRead(filePath);
                manifest.SaveAsset(newFile, entryStream);
            }
        }
    }

    private void InstallPackages(IEnumerable<NuGetPackage> packages, ICompiler compiler)
    {
        foreach (NuGetPackage package in packages)
            InstallPackage(package, compiler);
    }

    private void InstallPackage(NuGetPackage package, ICompiler compiler)
    {
        foreach (NuGetPackage.Item file in package.LibFiles)
        {
            AssetFile newFile = manifest.CreateAsset(file.FilePath, AssetFile.AssetType.Lib);
            if (!manifest.IsAssetValid(newFile))
            {
                using Stream entryStream = File.OpenRead(file.FullPath);
                manifest.SaveAsset(newFile, entryStream);
            }

            if (Path.GetDirectoryName(newFile.FullPath) == newFile.BaseDir)
                compiler.TryAddDependency(newFile.FullPath);
        }

        foreach (NuGetPackage.Item file in package.ContentFiles)
        {
            AssetFile newFile = manifest.CreateAsset(file.FilePath, AssetFile.AssetType.LibContent);
            if (!manifest.IsAssetValid(newFile))
            {
                using Stream entryStream = File.OpenRead(file.FullPath);
                manifest.SaveAsset(newFile, entryStream);
            }
        }
    }

    private bool IsAssetZipPath(string path, out string assetFilePath)
    {
        assetFilePath = null;

        if (path.EndsWith("/") || string.IsNullOrEmpty(AssetFolder))
            return false;

        if (
            path.StartsWith(AssetFolder, StringComparison.Ordinal)
            && path.Length > (AssetFolder.Length + 1)
        )
        {
            assetFilePath = path.Substring(AssetFolder.Length).TrimStart('/');
            return true;
        }
        return false;
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

    private string RemoveRoot(string path)
    {
        return path.Replace('\\', '/').Replace(manifest.SrcDir.Replace('\\', '/'), "").TrimStart('/');
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
            manifest.Invalidate();
            LogFile.WriteLine(
                $"Cache for GitHub plugin {RepoId} was invalidated, it will need to be compiled again at next game start"
            );
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to invalidate github cache: " + e);
        }
    }

    public override string GetAssetPath()
    {
        if (string.IsNullOrEmpty(AssetFolder))
            return null;
        return Path.GetFullPath(manifest.AssetFolder);
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
