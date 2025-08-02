using System.IO;
using System.Reflection;
using System.Threading;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;

namespace Pulsar.Legacy.Launcher
{
    static class Program
    {
        private const string OriginalAssemblyFile = "SpaceEngineers.exe";
        private const string ProgramGuid = "03f85883-4990-4d47-968e-5e4fc5d72437";

        static void Main(string[] args)
        {
            // Executable is re-launched by SE with this flag when displaying a
            // crash report. Might allow plugins to edit this in the future.
            if (Tools.HasCommandArg("-report") || Tools.HasCommandArg("-reporX"))
            {
                Game.StartSpaceEngineers(args);
                return;
            }

            string exeLocation = Path.GetDirectoryName(
                Path.GetFullPath(Assembly.GetExecutingAssembly().Location)
            );
            string pulsarDir = Path.Combine(exeLocation, "Pulsar");

            // This sets up a lot of classes and should be called ASAP
            // It's a code smell but had to be done in order to split PluginLoader into multiple projects
            new ConfigManager(pulsarDir, new Dependency(), Loader.DebugCompileAll);

            LogFile.WriteLine(
                "Starting Pulsar v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3)
            );

            Game.SetupMyFakes();
            Game.CorrectExitText();

            if (!Tools.HasCommandArg("-nosplash"))
                SplashManager.Instance = new SplashManager();

            if (!Tools.HasCommandArg("-keepintro"))
                Game.ShowIntroVideo(false);

            var launcher = new Shared.Launcher(ProgramGuid, OriginalAssemblyFile, pulsarDir);
            if (!launcher.CanStart())
                return;

            Loader.Instance = new Loader();

            Steam.EnsureAppID();
            Steam.StartSteam();

            Game.RegisterPlugin(typeof(Plugin.Main).Assembly);
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
