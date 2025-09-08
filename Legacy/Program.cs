using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HarmonyLib;
using Pulsar.Legacy.Launcher;
using Pulsar.Legacy.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using SharedLauncher = Pulsar.Shared.Launcher;
using SharedLoader = Pulsar.Shared.Loader;

namespace Pulsar.Legacy;

static class Program
{
    class ExternalTools : IExternalTools
    {
        public void OnMainThread(Action action) => Game.RunOnGameThread(action);
    }

    private const string OriginalAssemblyFile = "SpaceEngineers.exe";
    private const string PulsarRepo = "SpaceGT/Pulsar";
    private const string SeVersion = "1.207.20";

    static void Main(string[] args)
    {
        Application.EnableVisualStyles();

        if (SharedLauncher.IsOtherPulsarRunning())
        {
            Tools.ShowMessageBox("Error: Pulsar is already running!");
            return;
        }

        string bin64Dir = Folder.GetBin64();
        if (bin64Dir is null)
        {
            Tools.ShowMessageBox(
                $"Error: {OriginalAssemblyFile} not found!\n"
                    + "You can specify a custom location with \"-bin64\""
            );
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += Game.GameAssemblyResolver(bin64Dir);
        string originalLoaderPath = Path.Combine(bin64Dir, OriginalAssemblyFile);

        // Executable is re-launched by SE with this flag when displaying a crash report.
        // TODO: Replace this with a Pulsar crash screen in the future.
        if (Tools.HasCommandArg("-report") || Tools.HasCommandArg("-reporX"))
        {
            Game.SetMainAssembly(Assembly.ReflectionOnlyLoadFrom(originalLoaderPath));
            Game.StartSpaceEngineers(args);
            return;
        }

        if (Tools.HasCommandArg("-debug"))
            Debugger.Launch();

        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        AssemblyName currentName = currentAssembly.GetName();

        string currentDir = Path.GetDirectoryName(Path.GetFullPath(currentAssembly.Location));
        string pulsarDir = Path.Combine(currentDir, currentName.Name);

        LogFile.Init(pulsarDir);
        LogFile.WriteLine("Starting Pulsar v" + currentName.Version.ToString(3));

        if (!Tools.HasCommandArg("-nosplash"))
            SplashManager.Instance = new SplashManager();

        SplashManager.Instance?.SetTitle("Pulsar");
        SplashManager.Instance?.SetText("Starting Pulsar...");

        string libraryDir = Path.Combine(currentDir, "Libraries");
        string dependencyDir = Path.Combine(libraryDir, currentName.Name);
        string modDir = Path.Combine(
            bin64Dir,
            @"..\..\..\workshop\content",
            Steam.AppId.ToString()
        );

        Version seVersion = Game.GetGameVersion(bin64Dir);

        // The ConfigManager singleton is used by most of the
        // shared project and must be initialized beforehand.
        new ConfigManager(
            pulsarDir,
            bin64Dir,
            modDir,
            seVersion,
            Tools.HasCommandArg("-debugCompileAll")
        );

        bool seMismatch = seVersion != new Version(SeVersion);
        bool noUpdate = Tools.HasCommandArg("-noupdate");
        Updater updater = new(PulsarRepo, seMismatch, noUpdate);
        if (updater.ShouldUpdate())
            updater.Update();

        string checkSum = null;
        string checkFile = Path.Combine(currentDir, "checksum.txt");

        if (Tools.HasCommandArg("-mkcheck"))
        {
            UTF8Encoding encoding = new();
            checkSum = Tools.GetFolderHash(libraryDir);
            File.WriteAllText(checkFile, checkSum, encoding);
        }
        else if (File.Exists(checkFile))
            checkSum = File.ReadAllText(checkFile);

        var launcher = new SharedLauncher(originalLoaderPath, libraryDir, checkSum);
        if (!launcher.Verify(noUpdate))
        {
            updater.Update();
            return;
        }

        if (!launcher.CanStart())
            return;

        SplashManager.Instance?.SetText("Starting Steam...");
        Steam.Init();

        SplashManager.Instance?.SetText("Getting Plugins...");

        using (CompilerFactory compiler = new([bin64Dir, dependencyDir], bin64Dir, pulsarDir))
        {
            // The AppDomain must be created ASAP if running under Mono
            // as Mono does not isolate assemblies properly.
            if (!Tools.IsNative())
                compiler.Init();

            Tools.Init(new ExternalTools(), compiler);
            SharedLoader.Instance = new SharedLoader();
        }

        Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Item2));
        if (preloader.HasPatches && !ConfigManager.Instance.SafeMode)
        {
            SplashManager.Instance?.SetText("Applying Preloaders...");
            preloader.Preload(bin64Dir, Path.Combine(pulsarDir, "Preloader"));
        }

        LogFile.GameLog = new GameLog();

        Game.SetMainAssembly(Assembly.ReflectionOnlyLoadFrom(originalLoaderPath));

        new Harmony(currentName.Name + ".Early").PatchCategory("Early");

        Game.SetupMyFakes();
        Game.CorrectExitText();

        if (!Tools.HasCommandArg("-keepintro"))
            Game.ShowIntroVideo(false);

        // This call is wrapped so that Keen references are not loaded prematurely
        ((Action)(() => Game.RegisterPlugin(new PluginLoader())))();

        SplashManager.Instance?.SetText("Launching Space Engineers...");
        if (Tools.IsNative())
            ProgressPollFactory().Start();

        Game.StartSpaceEngineers(args);
    }

    private static Thread ProgressPollFactory()
    {
        return new Thread(() =>
        {
            float progress = 0;
            SplashManager splash = SplashManager.Instance;

            while (SplashManager.Instance is not null && progress < 1)
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
