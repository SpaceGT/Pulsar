using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Pulsar.Compiler;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;

namespace Pulsar.Shared.Data
{
    public class LocalFolderPlugin : PluginData
    {
        const int GitTimeout = 10000;

        public override bool IsLocal => true;
        public override bool IsCompiled => true;
        private string[] sourceDirectories;
        private GitHubPlugin github;
        private AssemblyResolver resolver;

        public LocalFolderConfig FolderSettings { get; private set; }

        public LocalFolderPlugin(string folder)
        {
            Id = folder;
            Status = PluginStatus.None;
            FolderSettings = new LocalFolderConfig() { Id = folder };
        }

        public override bool LoadData(ref PluginDataConfig config, bool enabled)
        {
            if (
                config is LocalFolderConfig folderConfig
                && folderConfig.DataFile != null
                && File.Exists(folderConfig.DataFile)
            )
            {
                FolderSettings = folderConfig;
                return false;
            }

            config = FolderSettings;
            return true;
        }

        public override Assembly GetAssembly()
        {
            if (Directory.Exists(Id))
            {
                ICompiler compiler = RoslynCompiler.CompilerFactory(FolderSettings.DebugBuild);
                bool hasFile = false;

                if (github.NuGetReferences != null && github.NuGetReferences.HasPackages)
                    InstallDependencies(compiler);

                StringBuilder sb = new();
                sb.Append("Compiling files from ").Append(Id).Append(":").AppendLine();
                foreach (var file in GetProjectFiles(Id))
                {
                    using FileStream fileStream = File.OpenRead(file);
                    hasFile = true;
                    string name = file.Substring(Id.Length + 1, file.Length - (Id.Length + 1));
                    sb.Append(name).Append(", ");
                    compiler.Load(fileStream, file);
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
                resolver?.AddAllowedAssemblyName(assemblyName);
                Assembly a = Assembly.Load(data, symbols);
                Version = a.GetName().Version;
                return a;
            }

            throw new DirectoryNotFoundException("Unable to find directory '" + Id + "'");
        }

        private void InstallDependencies(ICompiler compiler)
        {
            NuGetPackageList packageList = github.NuGetReferences;
            NuGetClient nuget = new();

            string binDir = Path.Combine(
                ConfigManager.Instance.PulsarDir,
                "NuGet",
                "bin",
                Tools.GetStringHash(Path.GetFullPath(Id))
            );
            if (Directory.Exists(binDir))
                Directory.Delete(binDir, true);
            Directory.CreateDirectory(binDir);

            if (!string.IsNullOrWhiteSpace(packageList.Config))
            {
                string nugetFile = Path.GetFullPath(Path.Combine(Id, packageList.Config));
                if (File.Exists(nugetFile))
                {
                    NuGetPackage[] packages;
                    using (FileStream fileStream = File.OpenRead(nugetFile))
                    {
                        packages = nuget.DownloadFromConfig(fileStream);
                    }
                    foreach (NuGetPackage package in packages)
                        InstallPackage(package, compiler, binDir);
                }
            }

            if (packageList.PackageIds != null)
            {
                foreach (NuGetPackage package in nuget.DownloadPackages(packageList.PackageIds))
                    InstallPackage(package, compiler, binDir);
            }

            resolver = new AssemblyResolver();
            resolver.AddSourceFolder(binDir);
        }

        private void InstallPackage(NuGetPackage package, ICompiler compiler, string binDir)
        {
            foreach (NuGetPackage.Item file in package.LibFiles)
            {
                string newFile = Path.Combine(binDir, file.FilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(newFile));
                File.Copy(file.FullPath, newFile);
                if (Path.GetDirectoryName(newFile) == binDir)
                    compiler.TryAddDependency(newFile);
            }

            foreach (NuGetPackage.Item file in package.ContentFiles)
            {
                string newFile = Path.Combine(binDir, file.FilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(newFile));
                File.Copy(file.FullPath, newFile);
            }
        }

        private IEnumerable<string> GetProjectFiles(string folder)
        {
            string gitError = null;
            try
            {
                Process p = new();

                // Redirect the output stream of the child process.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = "git";
                p.StartInfo.Arguments = "ls-files --cached --others --exclude-standard";
                p.StartInfo.WorkingDirectory = folder;
                p.Start();

                // Do not wait for the child process to exit before
                // reading to the end of its redirected stream.
                // Read the output stream first and then wait.
                string gitOutput = p.StandardOutput.ReadToEnd();
                gitError = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(GitTimeout))
                {
                    p.Kill();
                    throw new TimeoutException("Git operation timed out.");
                }

                if (p.ExitCode == 0)
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
            if (sourceDirectories == null || sourceDirectories.Length == 0)
                return true;
            file = file.Replace('\\', '/');
            foreach (string dir in sourceDirectories)
            {
                if (file.StartsWith(dir))
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Id;
        }

        public void LoadNewDataFile(Action onComplete)
        {
            Tools.OpenFileDialog(
                "Open an xml data file",
                Path.GetDirectoryName(FolderSettings.DataFile),
                Tools.XmlDataType,
                (file) =>
                {
                    DeserializeFile(file);
                    onComplete.Invoke();
                }
            );
        }

        // Deserializes a data file
        public void DeserializeFile(string file)
        {
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
                sourceDirectories = github
                    .SourceDirectories?.Select(x => Path.Combine(Id, x).Replace('\\', '/'))
                    .ToArray();
                FolderSettings.DataFile = file;
                this.github = github;
            }
            catch (Exception e)
            {
                LogFile.Error($"Error while reading the xml file {file} for {Id}: " + e);
            }
        }

        public override string GetAssetPath()
        {
            if (string.IsNullOrEmpty(github.AssetFolder))
                return null;
            return Path.GetFullPath(Path.Combine(Id, github.AssetFolder));
        }
    }
}
