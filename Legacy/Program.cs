using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Pulsar.Legacy.Launcher;
using Pulsar.Legacy.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using SharedLauncher = Pulsar.Shared.Launcher;
using SharedLoader = Pulsar.Shared.Loader;

namespace Pulsar.Legacy
{
    static class Program
    {
        class Dependency : IDependency
        {
            public void OnMainThread(Action action) => Game.RunOnGameThread(action);
        }

        private const string OriginalAssemblyFile = "SpaceEngineers.exe";
        private const string PulsarRepo = "SpaceGT/Pulsar";
        private const string SeVersion = "1.206.32";

        static void Main(string[] args)
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            AssemblyName currentName = currentAssembly.GetName();

            // Executable is re-launched by SE with this flag when displaying a
            // crash report. Might allow plugins to edit this in the future.
            if (Tools.HasCommandArg("-report") || Tools.HasCommandArg("-reporX"))
            {
                Game.StartSpaceEngineers(args);
                return;
            }

            if (Tools.HasCommandArg("-debug"))
                Debugger.Launch();

            LogFile.WriteLine("Starting Pulsar v" + currentName.Version.ToString(3));

            if (!Tools.HasCommandArg("-nosplash"))
                SplashManager.Instance = new SplashManager();

            SplashManager.Instance?.SetText("Starting Pulsar...");

            string currentDir = Path.GetDirectoryName(Path.GetFullPath(currentAssembly.Location));
            string pulsarDir = Path.Combine(currentDir, currentName.Name);
            string dependencyDir = Path.Combine(currentDir, "Libraries");
            string bin64Dir = Folder.GetBin64();

            if (bin64Dir is null)
            {
                Tools.ShowMessageBox(
                    $"Error: {OriginalAssemblyFile} not found!\n"
                        + "You can specify a custom location with \"-bin64\""
                );
                return;
            }

            string modDir = Path.Combine(
                bin64Dir,
                @"..\..\..\workshop\content",
                Steam.AppId.ToString()
            );

            AppDomain.CurrentDomain.AssemblyResolve += Game.GameAssemblyResolver(bin64Dir);

            LogFile.Init(pulsarDir);

            Version seVersion = Game.GetGameVersion(bin64Dir);

            // This must be called before using most of the Shared project
            new ConfigManager(
                pulsarDir,
                bin64Dir,
                modDir,
                seVersion,
                new Dependency(),
                Tools.HasCommandArg("-debugCompileAll")
            );

            bool outdated = seVersion != new Version(SeVersion);
            Updater.CheckUpdate(PulsarRepo, outdated);

            string checkSum = null;
            string checkFile = Path.Combine(currentDir, "checksum.txt");
            if (File.Exists(checkFile))
                checkSum = File.ReadAllText(checkFile);

            string originalLoader = Path.Combine(bin64Dir, OriginalAssemblyFile);
            var launcher = new SharedLauncher(originalLoader, dependencyDir, checkSum);
            if (!launcher.CanStart())
                return;

            SplashManager.Instance?.SetText("Starting Steam...");
            Steam.Init();

            SplashManager.Instance?.SetText("Getting Plugins...");
            SharedLoader.Instance = new SharedLoader(References.GetReferences(bin64Dir));

            Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Item2));
            if (preloader.HasPatches)
            {
                SplashManager.Instance?.SetText("Applying Preloaders...");
                preloader.Preload(bin64Dir, Path.Combine(pulsarDir, "Preloader"));
            }

            LogFile.GameLog = new GameLog();

            Assembly originalLauncher = Assembly.ReflectionOnlyLoadFrom(
                Path.Combine(bin64Dir, "SpaceEngineers.exe")
            );
            Game.SetMainAssembly(originalLauncher);

            new Harmony(currentName.Name + ".Early").PatchCategory("Early");

            Game.SetupMyFakes();
            Game.CorrectExitText();

            if (!Tools.HasCommandArg("-keepintro"))
                Game.ShowIntroVideo(false);

            // This call is wrapped so that Keen references are not loaded prematurely
            ((Action)(() => Game.RegisterPlugin(new PluginLoader())))();

            ProgressPollFactory().Start();

            SplashManager.Instance?.SetText("Launching Space Engineers...");
            try
            {
                Game.StartSpaceEngineers(args);
            }
            finally
            {
                launcher.ReleaseMutex();
            }
        }

        private static Thread ProgressPollFactory()
        {
            return new Thread(() =>
            {
                float progress = 0;
                SplashManager splash = SplashManager.Instance;

                while (splash is not null && progress < 1)
                {
                    // FIXME: Does not work well with preloaded assemblies
                    progress = Game.GetLoadProgress();

                    if (float.IsNaN(splash.BarValue) || splash.BarValue < progress)
                        splash?.SetBarValue(progress);

                    Thread.Sleep(250); // ms
                }
            })
            {
                IsBackground = true,
                Name = "ProgressPoll",
            };
        }
    }
}
