using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Pulsar.Compiler;
using Pulsar.Protocol.Interface;
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

        string file;
        if (folderConfig.DataFile is null)
            file = null;
        else if (!Path.IsPathRooted(folderConfig.DataFile))
            file = Path.Combine(Folder, folderConfig.DataFile);
        else
            file = folderConfig.DataFile;

        settings = Tools.DeepCopy(folderConfig);
        DeserializeFile(file);
    }

    public override Assembly GetAssembly()
    {
        if (!Directory.Exists(Folder))
            throw new DirectoryNotFoundException("Unable to find directory '" + Folder + "'");

        bool debug = settings.DebugBuild;
        ICompiler compiler = Tools.Compiler.Create(debug);
        bool hasFile = false;

        string binDir = Path.Combine(
            ConfigManager.Instance.PulsarDir,
            "NuGet",
            "bin",
            Tools.GetStringHash(Path.GetFullPath(Folder))
        );
        if (Directory.Exists(binDir))
            Directory.Delete(binDir, true);
        Directory.CreateDirectory(binDir);

        if (github?.NuGetReferences is not null && github.NuGetReferences.HasPackages)
            InstallDependencies(compiler, binDir);

        StringBuilder sb = new();
        sb.Append("Compiling files from ").Append(Folder).Append(':').AppendLine();

        IEnumerable<string> projectFiles = GetProjectFilesGit(Folder);
        projectFiles ??= GetProjectFilesFallback(Folder);

        foreach (var file in projectFiles)
        {
            using FileStream fileStream = File.OpenRead(file);
            hasFile = true;
            string name = file.Substring(Folder.Length + 1, file.Length - (Folder.Length + 1));
            sb.Append(name).Append(", ");
            string relFile = GetRelativePath(file);
            compiler.Load(fileStream, relFile, debug ? file : null);
        }

        if (hasFile)
        {
            sb.Length -= 2;
            LogFile.WriteLine(sb.ToString());
        }
        else
        {
            throw new IOException("No files were found in the directory specified.");
        }

        string assemblyName = FriendlyName + '_' + Path.GetRandomFileName();
        byte[] data = compiler.Compile(assemblyName, out byte[] symbols);
        string dll = Path.Combine(binDir, NuGetRestore.PluginFileName);
        File.WriteAllBytes(dll, data);

        if (symbols is not null)
        {
            string pdbFile = Path.Combine(binDir, Path.ChangeExtension(assemblyName, "pdb"));
            File.WriteAllBytes(pdbFile, symbols);
        }

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

    public void LoadNewDataFile(Action<string> onComplete = null)
    {
        Tools.OpenFileDialog(
            "Open an xml data file",
            Folder,
            [
                new FilePickerFilter { Name = "Xml files (*.xml)", Patterns = ["*.xml"] },
                new FilePickerFilter { Name = "All files (*.*)", Patterns = ["*.*"] },
            ],
            (file) =>
            {
                DeserializeFile(file);
                onComplete?.Invoke(settings.DataFile);
            }
        );
    }

    public void DeserializeFile(string file)
    {
        if (file is null)
        {
            github = null;
            FriendlyName = Id;
            settings.DataFile = null;
            Tooltip = null;
            Author = null;
            Description = null;
            Runtimes = null;
            Platforms = null;
            DependencyIds = null;
            return;
        }

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
            github.InitPaths();
            FriendlyName = github.FriendlyName;
            Tooltip = github.Tooltip;
            Author = github.Author;
            Description = github.Description;
            Runtimes = github.Runtimes;
            Platforms = github.Platforms;
            DependencyIds = github.DependencyIds;

            sourceDirectories = github
                .SourceDirectories?.Select(x => Path.Combine(Folder, x).Replace('\\', '/'))
                .ToArray();

            if (file.Contains(Folder))
                settings.DataFile = GetRelativePath(file);
            else
                settings.DataFile = file;

            this.github = github;
        }
        catch (Exception e)
        {
            LogFile.Error($"Error while reading the xml file {file} for {Folder}: " + e);
        }
    }

    public override string GetAssetPath()
    {
        if (string.IsNullOrEmpty(github?.AssetFolder))
            return null;

        return Path.GetFullPath(Path.Combine(Folder, github.AssetFolder));
    }

    private string GetRelativePath(string file) =>
        file.Replace(Folder, "").TrimStart(Path.DirectorySeparatorChar);
}
