using System;
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

            public void SteamSubscribe(ulong id) => Steam.SubscribeToItem(id);
        }

        private const string OriginalAssemblyFile = "SpaceEngineers.exe";
        private const string ProgramGuid = "03f85883-4990-4d47-968e-5e4fc5d72437";

        static void Main(string[] args)
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();

            // Executable is re-launched by SE with this flag when displaying a
            // crash report. Might allow plugins to edit this in the future.
            if (Tools.HasCommandArg("-report") || Tools.HasCommandArg("-reporX"))
            {
                Game.StartSpaceEngineers(args);
                return;
            }

            LogFile.WriteLine("Starting Pulsar v" + currentAssembly.GetName().Version.ToString(3));

            if (!Tools.HasCommandArg("-nosplash"))
                SplashManager.Instance = new SplashManager();

            SplashManager.Instance?.SetText("Starting Pulsar...");

            string pulsarDir = Path.GetDirectoryName(Path.GetFullPath(currentAssembly.Location));
            string bin64Dir = Path.Combine(pulsarDir, "..");
            string modDir = Path.Combine(
                bin64Dir,
                @"..\..\..\workshop\content",
                Steam.AppId.ToString()
            );

            AppDomain.CurrentDomain.AssemblyResolve += Game.GameAssemblyResolver(bin64Dir);

            LogFile.Init(pulsarDir);

            SplashManager.Instance?.SetText("Starting Steam...");

            Steam.EnsureAppID();
            Steam.StartSteam();

            // This must be called before using most of the Shared project
            new ConfigManager(
                pulsarDir,
                modDir,
                Steam.GetSteamId(),
                Game.GetGameVersion(bin64Dir),
                new Dependency(),
                SharedLoader.DebugCompileAll
            );

            string originalLoader = Path.Combine(bin64Dir, OriginalAssemblyFile);
            var launcher = new SharedLauncher(ProgramGuid, originalLoader, pulsarDir);
            if (!launcher.CanStart())
                return;

            SplashManager.Instance?.SetText("Getting Plugins...");
            SharedLoader.Instance = new SharedLoader(References.GetReferences(bin64Dir));

            Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Item2));
            if (preloader.HasPatches)
            {
                SplashManager.Instance?.SetText("Applying Preloaders...");
                preloader.Preload(Path.Combine(pulsarDir, "Preloader"));
            }

            LogFile.GameLog = new GameLog();

            Assembly originalLauncher = Assembly.ReflectionOnlyLoadFrom(
                Path.Combine(bin64Dir, "SpaceEngineers.exe")
            );
            Game.SetMainAssembly(originalLauncher);

            new Harmony(currentAssembly.GetName().Name).PatchAll(currentAssembly);

            Game.SetupMyFakes();
            Game.CorrectExitText();

            if (!Tools.HasCommandArg("-keepintro"))
                Game.ShowIntroVideo(false);

            // This call is wrapped so that Keen references are not loaded prematurely
            ((Action)(() => Game.RegisterPlugin(new Plugin())))();

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

                while (splash != null && progress < 1)
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
