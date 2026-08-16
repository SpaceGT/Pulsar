using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Pulsar.Compiler;
using Pulsar.Shared.Assets;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;

namespace Pulsar.Shared.Data;

public class LocalFolderPlugin : PluginData
{
    const int GitTimeout = 10000;

    public override bool IsLocal => true;
    public override bool IsCompiled => true;
    private string[] sourceDirectories;
    private GitHubPlugin github;
    private LocalFolderConfig settings;

    public string Folder;

    public LocalFolderPlugin(string folder)
    {
        Id = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        Folder = folder;
        Status = PluginStatus.None;
        FriendlyName = Id;
        settings = new() { Id = Id };
    }

    public override string ToString() => Id;

    public override void LoadData(PluginDataConfig config)
    {
        if (config is not LocalFolderConfig folderConfig)
            return;

        settings = Tools.DeepCopy(folderConfig);
    }

    public override Assembly GetAssembly()
    {
        if (!Directory.Exists(Folder))
            throw new DirectoryNotFoundException("Unable to find directory '" + Folder + "'");

        PluginAsset[] assets = github?.Assets ?? [];
        string[] projectFiles = [.. GetProjectFilesGit(Folder) ?? GetProjectFilesFallback(Folder)];
        if (projectFiles.Length == 0)
            throw new IOException("No files were found in the directory specified.");

        bool debug = settings.DebugBuild;
        PluginCache cache = PluginCache.Load(GetCacheDirectory());
        string binDir = cache.BinDirectory;
        string dll = cache.DllFile;
        string cacheHash = GetCacheHash(projectFiles, debug, assets);

        if (cache.IsValid(cacheHash, ConfigManager.Instance.GameVersion))
        {
            namedAssets = cache.GetAssets();
            return LoadAssembly(dll);
        }

        cache.Clear();
        Directory.CreateDirectory(binDir);

        ICompiler compiler = Tools.Compiler.Create(debug);
        if (github?.NuGetReferences is not null && github.NuGetReferences.HasPackages)
            InstallDependencies(compiler, binDir);

        AssetResolver rebuildResolver = new(cache);
        AssetResolution resolution = rebuildResolver.Resolve(assets, anchor: Folder);
        foreach (string reference in resolution.References)
            compiler.TryAddDependency(reference);

        StringBuilder sb = new();
        sb.Append("Compiling files from ").Append(Folder).Append(':').AppendLine();

        foreach (string file in projectFiles)
        {
            using FileStream fileStream = File.OpenRead(file);
            string relFile = GetRelativePath(file);
            sb.Append(relFile).Append(", ");
            compiler.Load(fileStream, relFile, debug ? file : null);
        }

        sb.Length -= 2;
        LogFile.WriteLine(sb.ToString());

        string assemblyName = FriendlyName + '_' + Path.GetRandomFileName();
        byte[] data = compiler.Compile(assemblyName, out byte[] symbols);
        File.WriteAllBytes(dll, data);

        if (symbols is not null)
        {
            string pdbFile = Path.Combine(binDir, Path.ChangeExtension(assemblyName, "pdb"));
            File.WriteAllBytes(pdbFile, symbols);
        }

        cache.SetAssets(resolution.Assets);
        cache.Save(cacheHash, ConfigManager.Instance.GameVersion);
        namedAssets = resolution.Assets;
        return LoadAssembly(dll);
    }

    private string GetCacheDirectory()
    {
        string folderHash = Tools.GetStringHash(Path.GetFullPath(Folder));
        string cacheName = $"{Tools.CleanFileName(Id)}-{folderHash.Substring(0, 8)}";
        return Path.Combine(ConfigManager.Instance.PulsarDir, "DevFolder", cacheName);
    }

    private string GetCacheHash(IEnumerable<string> projectFiles, bool debug, PluginAsset[] assets)
    {
        string sources = string.Join(
            "\n",
            projectFiles
                .OrderBy(GetRelativePath, StringComparer.Ordinal)
                .Select(file => GetRelativePath(file) + ":" + Tools.GetFileHash(file))
        );

        string context = string.Join(
            "\n",
            sources,
            FriendlyName,
            debug,
            github?.NuGetReferences?.GetFingerprint(),
            AssetResolver.GetDevfolderFingerprint(assets, Folder)
        );

        return Tools.GetStringHash(context);
    }

    private Assembly LoadAssembly(string dll)
    {
        Assembly a = Assembly.LoadFrom(dll);
        Version = a.GetName().Version;
        return a;
    }

    private void InstallDependencies(ICompiler compiler, string binDir)
    {
        NuGetPackageList packageList = github.NuGetReferences;
        NuGetRestoreResult restore = NuGetRestore.Run(packageList);

        foreach (string file in restore.CompileFiles)
            compiler.TryAddDependency(file);

        foreach (NuGetRestoreFile file in restore.RuntimeFiles)
        {
            string newFile = Path.Combine(binDir, file.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(newFile));
            File.Copy(file.SourcePath, newFile);
        }
    }

    private IEnumerable<string> GetProjectFilesGit(string folder)
    {
        string gitError = null;
        try
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "git",
                Arguments = "ls-files --cached --others --exclude-standard",
                WorkingDirectory = folder,
            };

            using Process process = new();
            process.StartInfo = startInfo;
            process.Start();

            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // Read the output stream first and then wait.
            string gitOutput = process.StandardOutput.ReadToEnd();
            gitError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(GitTimeout))
            {
                process.Kill();
                throw new TimeoutException("Git operation timed out.");
            }

            if (process.ExitCode == 0)
            {
                string[] files = gitOutput.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
                return files
                    .Where(x => x.EndsWith(".cs"))
                    .Select(x =>
                        Path.Combine(folder, x.Trim().Replace('/', Path.DirectorySeparatorChar))
                    )
                    .Where(x => IsValidProjectFile(x) && File.Exists(x));
            }
            else
            {
                StringBuilder sb = new StringBuilder(
                    "An error occurred while checking git for project files."
                ).AppendLine();
                if (!string.IsNullOrWhiteSpace(gitError))
                {
                    sb.AppendLine("Git output: ");
                    sb.Append(gitError).AppendLine();
                }
                LogFile.WriteLine(sb.ToString());
            }
        }
        catch (Exception e)
        {
            StringBuilder sb = new StringBuilder(
                "An error occurred while checking git for project files."
            ).AppendLine();
            if (!string.IsNullOrWhiteSpace(gitError))
            {
                sb.AppendLine(" Git output: ");
                sb.Append(gitError).AppendLine();
            }
            sb.AppendLine("Exception: ");
            sb.Append(e).AppendLine();
            LogFile.WriteLine(sb.ToString());
        }

        return null;
    }

    private IEnumerable<string> GetProjectFilesFallback(string folder)
    {
        LogFile.Warn("Using fallback search for project files!");
        char sep = Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(x =>
                !x.Contains(sep + "bin" + sep)
                && !x.Contains(sep + "obj" + sep)
                && IsValidProjectFile(x)
            );
    }

    private bool IsValidProjectFile(string file)
    {
        if (sourceDirectories is null || sourceDirectories.Length == 0)
            return true;
        file = file.Replace('\\', '/');
        foreach (string dir in sourceDirectories)
        {
            if (file.StartsWith(dir))
                return true;
        }
        return false;
    }

    public override void UpdateProfile(Profile draft, bool enabled)
    {
        base.UpdateProfile(draft, enabled);

        if (enabled)
            draft.DevFolder.Add(new() { Id = Id });
    }

    public override void InvalidateCache()
    {
        try
        {
            string cacheDirectory = GetCacheDirectory();
            if (Directory.Exists(cacheDirectory))
                PluginCache.Load(cacheDirectory).Invalidate();
        }
        catch (Exception e)
        {
            LogFile.Error("Failed to invalidate dev folder cache: " + e);
        }
    }

    internal void TryLoadDataFile(string file)
    {
        if (file is null)
            return;

        if (!Path.IsPathRooted(file))
            file = Path.Combine(Folder, file);

        if (!File.Exists(file))
            return;

        try
        {
            XmlSerializer xml = new(typeof(PluginData));

            using StreamReader reader = File.OpenText(file);
            object resultObj = xml.Deserialize(reader);
            if (resultObj.GetType() != typeof(GitHubPlugin))
            {
                throw new Exception("Xml file is not of type GitHubPlugin!");
            }

            GitHubPlugin github = (GitHubPlugin)resultObj;
            FriendlyName = github.FriendlyName;
            Tooltip = github.Tooltip;
            Author = github.Author;
            Description = github.Description;
            Runtimes = github.Runtimes;
            Platforms = github.Platforms;
            DependencyIds = github.DependencyIds;
            sourceDirectories = github
                .SourceDirectories?.Select(path =>
                    Path.Combine(Folder, path).Replace('\\', '/').TrimEnd('/') + "/"
                )
                .ToArray();

            this.github = github;
        }
        catch (Exception e)
        {
            LogFile.Error($"Error while reading the xml file {file} for {Folder}: " + e);
        }
    }

    private string GetRelativePath(string file) => Tools.GetRelativePath(Folder, file) ?? file;
}
