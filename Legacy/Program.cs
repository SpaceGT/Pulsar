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
using Pulsar.Legacy.Patch;
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

    private const string PulsarRepo = "SpaceGT/Pulsar";
    private const string OldLauncher = "SpaceEngineers.exe";

    static void Main(string[] args)
    {
        Application.EnableVisualStyles();

        if (SharedLauncher.IsOtherPulsarRunning())
        {
            Tools.ShowMessageBox("Error: Pulsar is already running!");
            return;
        }

        if (Flags.ExternalDebug)
            Debugger.Launch();

        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        string baseDir = Path.GetDirectoryName(currentAssembly.Location);

        SetupCoreData(args, baseDir);
        Updater updater = TryUpdate(baseDir);
        SetupGameData(updater);
        CheckCanStart(updater);
        SetupSteam();
        SetupPlugins(baseDir);
        SetupGameResolver();
        SetupGame(args);
    }

    private static void SetupCoreData(string[] args, string baseDir)
    {
        var asmName = Assembly.GetExecutingAssembly().GetName();
        string pulsarDir = Path.Combine(baseDir, asmName.Name);

        LogFile.Init(pulsarDir);
        LogFile.WriteLine($"Starting Pulsar v{asmName.Version.ToString(3)}");

        Flags.LogFlags();

        if (Flags.SplashType == SplashType.Pulsar)
            SplashManager.Instance = new SplashManager();

        SplashManager.Instance?.SetTitle("Pulsar");
        SplashManager.Instance?.SetText("Starting Pulsar...");

        ConfigManager.EarlyInit(pulsarDir);
    }

    private static Updater TryUpdate(string baseDir)
    {
        Updater updater = new(PulsarRepo);
        updater.TryUpdate();

        string checkSum = null;
        string checkFile = Path.Combine(baseDir, "checksum.txt");
        string libraryDir = Path.Combine(baseDir, "Libraries");

        if (Flags.MakeCheckFile)
        {
            UTF8Encoding encoding = new();
            checkSum = Tools.GetFolderHash(libraryDir);
            File.WriteAllText(checkFile, checkSum, encoding);
        }
        else if (File.Exists(checkFile))
            checkSum = File.ReadAllText(checkFile);

        if (checkSum is not null && Tools.GetFolderHash(libraryDir) != checkSum)
            updater.ShowBitrotPrompt();

        return updater;
    }

    private static void SetupGameData(Updater updater)
    {
        string bin64Dir = Folder.GetBin64();
        if (bin64Dir is null)
        {
            Tools.ShowMessageBox(
                $"Error: {OldLauncher} not found!\n"
                    + "You can specify a custom location with \"-bin64\""
            );
            Environment.Exit(1);
        }

        string modDir = Path.Combine(
            bin64Dir,
            @"..\..\..\workshop\content",
            Steam.AppId.ToString()
        );

        Version seVersion = Game.GetGameVersion(bin64Dir);
        if (seVersion is null) // Prevent NRE from Keen updates
            updater.ShowBitrotPrompt();

        ConfigManager.Init(bin64Dir, modDir, seVersion);

        CoreConfig coreConfig = ConfigManager.Instance.Core;
        Version oldSeVersion = coreConfig.GameVersion;
        if (seVersion != oldSeVersion)
        {
            if (oldSeVersion is not null)
                Updater.GameUpdatePrompt(oldSeVersion, seVersion);

            coreConfig.GameVersion = seVersion;
            coreConfig.Save();
        }
    }

    private static void CheckCanStart(Updater updater)
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(bin64Dir, OldLauncher);
        var launcher = new SharedLauncher(originalLoaderPath);
        if (!launcher.VerifyConfig())
            updater.ShowBitrotPrompt();

        if (!launcher.CanStart())
            Environment.Exit(1);
    }

    private static void SetupSteam()
    {
        SplashManager.Instance?.SetText("Starting Steam...");
        string bin64Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += Steam.SteamworksResolver(bin64Dir);
        Steam.Init();
    }

    private static void SetupPlugins(string baseDir)
    {
        SplashManager.Instance?.SetText("Getting Plugins...");

        var asmName = Assembly.GetExecutingAssembly().GetName();
        string dependencyDir = Path.Combine(baseDir, "Libraries", asmName.Name);

        string pulsarDir = ConfigManager.Instance.PulsarDir;
        string bin64Dir = ConfigManager.Instance.GameDir;

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
    }

    private static void SetupGameResolver()
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += Game.GameAssemblyResolver(bin64Dir);
    }

    private static void SetupGame(string[] args)
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(bin64Dir, OldLauncher);
        Patch_PrepareCrashReport.SpaceEngineersPath = originalLoaderPath;

        LogFile.GameLog = new GameLog();

        Game.SetMainAssembly(Assembly.ReflectionOnlyLoadFrom(originalLoaderPath));

        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        new Harmony(assemblyName + ".Early").PatchCategory("Early");

        Game.SetupMyFakes();
        Game.CorrectExitText();
        Game.ShowIntroVideo(Flags.GameIntroVideo);
        Game.RegisterPlugin(new PluginLoader());

        SplashManager.Instance?.SetText("Launching Space Engineers...");
        if (Tools.IsNative())
            ProgressPollFactory().Start();

        Game.StartSpaceEngineers(args);
    }

    private static Thread ProgressPollFactory()
    {
        static void ProgressPoll()
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
        }

        return new Thread(ProgressPoll) { IsBackground = true, Name = "ProgressPoll" };
    }
}
