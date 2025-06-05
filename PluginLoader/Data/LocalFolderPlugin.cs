using avaness.PluginLoader.Compiler;
using avaness.PluginLoader.Config;
using avaness.PluginLoader.GUI;
using avaness.PluginLoader.Network;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using VRage.FileSystem;

namespace avaness.PluginLoader.Data
{
    public class LocalFolderPlugin : PluginData
    {
        const string XmlDataType = "Xml files (*.xml)|*.xml|All files (*.*)|*.*";
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
            FolderSettings = new LocalFolderConfig()
            {
                Id = folder
            };
        }

        public override bool LoadData(ref PluginDataConfig config, bool enabled)
        {
            if (config is LocalFolderConfig folderConfig && folderConfig.DataFile != null && File.Exists(folderConfig.DataFile))
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
                RoslynCompiler compiler = new RoslynCompiler(FolderSettings.DebugBuild);
                bool hasFile = false;

                if (github.NuGetReferences != null && github.NuGetReferences.HasPackages)
                    InstallDependencies(compiler);

                StringBuilder sb = new StringBuilder();
                sb.Append("Compiling files from ").Append(Id).Append(":").AppendLine();
                foreach(var file in GetProjectFiles(Id))
                {
                    using (FileStream fileStream = File.OpenRead(file))
                    {
                        hasFile = true;
                        string name = file.Substring(Id.Length + 1, file.Length - (Id.Length + 1));
                        sb.Append(name).Append(", ");
                        compiler.Load(fileStream, file);
                    }
                }

                if(hasFile)
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

        private void InstallDependencies(RoslynCompiler compiler)
        {
            NuGetPackageList packageList = github.NuGetReferences;
            NuGetClient nuget = new NuGetClient();

            string binDir = Path.Combine(MyFileSystem.ExePath, "NuGet", "bin", LoaderTools.GetHashString256(Path.GetFullPath(Id)));
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

            if(packageList.PackageIds != null)
            {
                foreach (NuGetPackage package in nuget.DownloadPackages(packageList.PackageIds))
                    InstallPackage(package, compiler, binDir);
            }

            resolver = new AssemblyResolver();
            resolver.AddSourceFolder(binDir);
        }

        private void InstallPackage(NuGetPackage package, RoslynCompiler compiler, string binDir)
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
                Process p = new Process();

                // Redirect the output stream of the child process.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
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
                    string[] files = gitOutput.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return files.Where(x => x.EndsWith(".cs")).Select(x => Path.Combine(folder, x.Trim().Replace('/', Path.DirectorySeparatorChar))).Where(x => IsValidProjectFile(x) && File.Exists(x));
                }
                else
                {
                    StringBuilder sb = new StringBuilder("An error occurred while checking git for project files.").AppendLine();
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
                StringBuilder sb = new StringBuilder("An error occurred while checking git for project files.").AppendLine();
                if(!string.IsNullOrWhiteSpace(gitError))
                {
                    sb.AppendLine(" Git output: ");
                    sb.Append(gitError).AppendLine();
                }
                sb.AppendLine("Exception: ");
                sb.Append(e).AppendLine();
                LogFile.WriteLine(sb.ToString());
            }


            char sep = Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(x => !x.Contains(sep + "bin" + sep) && !x.Contains(sep + "obj" + sep) && IsValidProjectFile(x));
        }

        private bool IsValidProjectFile(string file)
        {
            if (sourceDirectories == null || sourceDirectories.Length == 0)
                return true;
            file = file.Replace('\\', '/');
            foreach(string dir in sourceDirectories)
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

        public override void Show()
        {
            string folder = Path.GetFullPath(Id);
            if (Directory.Exists(folder))
                Process.Start("explorer.exe", $"\"{folder}\"");
        }

        public void LoadNewDataFile(Action onComplete)
        {
            LoaderTools.OpenFileDialog("Open an xml data file", Path.GetDirectoryName(FolderSettings.DataFile), XmlDataType,
                (file) =>
                {
                    DeserializeFile(file);
                    onComplete.Invoke();
                });
        }

        // Deserializes a data file
        public void DeserializeFile(string file)
        {
            if (!File.Exists(file))
                return;

            try
            {
                XmlSerializer xml = new XmlSerializer(typeof(PluginData));

                using (StreamReader reader = File.OpenText(file))
                {
                    object resultObj = xml.Deserialize(reader);
                    if(resultObj.GetType() != typeof(GitHubPlugin))
                    {
                        throw new Exception("Xml file is not of type GitHubPlugin!");
                    }

                    GitHubPlugin github = (GitHubPlugin)resultObj;
                    github.InitPaths();
                    FriendlyName = github.FriendlyName;
                    Tooltip = github.Tooltip;
                    Author = github.Author;
                    Description = github.Description;
                    sourceDirectories = github.SourceDirectories?.Select(x => Path.Combine(Id, x).Replace('\\', '/')).ToArray();
                    FolderSettings.DataFile = file;
                    this.github = github;
                }
            }
            catch (Exception e)
            {
                LogFile.Error($"Error while reading the xml file {file} for {Id}: " + e);
            }
        }

        public static void PromptFolder(Action<string> onComplete)
        {
            LoaderTools.OpenFolderDialog("Open the root of your project", (folder) =>
            {
                if (Main.Instance.List.Contains(folder))
                {
                    MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, messageText: new StringBuilder("That development folder already exists!"), messageCaption: new StringBuilder("Plugin Loader")));
                    return;
                }

                LoaderTools.OpenFileDialog("Open the xml data file", folder, XmlDataType, (file) => 
                {
                    onComplete(file);
                });
            });
        }

        public override void AddDetailControls(PluginDetailMenu screen, MyGuiControlBase bottomControl, out MyGuiControlBase topControl)
        {
            MyGuiControlButton btnRemove = new MyGuiControlButton(text: new StringBuilder("Remove"), onButtonClick: (btn) =>
            {
                PluginConfig config = Main.Instance.Config;
                config.Save();
                screen.CloseScreen();
                screen.InvokeOnPluginRemoved(this);
                screen.InvokeOnRestartRequired();
            });
            screen.PositionAbove(bottomControl, btnRemove);
            screen.Controls.Add(btnRemove);

            MyGuiControlButton btnLoadFile = new MyGuiControlButton(text: new StringBuilder("Load File"), onButtonClick: (btn) =>
            {
                LoadNewDataFile(() =>
                {
                    screen.CloseScreen();
                });
            });
            screen.PositionToRight(btnRemove, btnLoadFile);
            btnLoadFile.Enabled = string.IsNullOrEmpty(FolderSettings.DataFile) || !File.Exists(FolderSettings.DataFile);
            screen.Controls.Add(btnLoadFile);

            MyGuiControlCombobox releaseDropdown = new MyGuiControlCombobox();
            releaseDropdown.AddItem(0, "Release");
            releaseDropdown.AddItem(1, "Debug");
            releaseDropdown.SelectItemByKey(FolderSettings.DebugBuild ? 1 : 0);
            releaseDropdown.ItemSelected += () =>
            {
                FolderSettings.DebugBuild = releaseDropdown.GetSelectedKey() == 1;
                Main.Instance.Config.Save();
                screen.InvokeOnRestartRequired();
            };
            screen.PositionAbove(btnRemove, releaseDropdown, MyAlignH.Left);
            screen.Controls.Add(releaseDropdown);
            topControl = releaseDropdown;
        }

        public override string GetAssetPath()
        {
            if (string.IsNullOrEmpty(github.AssetFolder))
                return null;
            return Path.GetFullPath(Path.Combine(Id, github.AssetFolder));
        }
    }
}
