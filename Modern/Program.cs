using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using HarmonyLib;
using Pulsar.Modern.Compiler;
using Pulsar.Modern.Launcher;
using Pulsar.Modern.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using Application = System.Windows.Forms.Application;
using SharedLauncher = Pulsar.Shared.Launcher;
using SharedLoader = Pulsar.Shared.Loader;
using Tools = Pulsar.Shared.Tools;

namespace Pulsar.Modern;

static class Program
{
    class ExternalTools : IExternalTools
    {
        public void OnMainThread(Action action) => throw new NotImplementedException();
    }

    private const string PulsarRepo = "SpaceGT/Pulsar";
    private const string OldLauncher = "SpaceEngineers2.dll";

    static void Main(string[] args)
    {
        string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string libraryDir = Path.Combine(baseDir, "Libraries", "Modern");
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([libraryDir, runtimeDir]);

        PulsarMain(args);
    }

    private static void PulsarMain(string[] args)
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

        SetupCoreData(baseDir);
        Updater updater = TryUpdate(baseDir);
        SetupGameData(updater);
        CheckCanStart();
        SetupSteam();
        SetupPlugins(baseDir);
        SetupGame(args);
    }

    private static void SetupCoreData(string baseDir)
    {
        Environment.CurrentDirectory = baseDir;

        var asmName = Assembly.GetExecutingAssembly().GetName();
        string pulsarDir = Path.Combine(baseDir, asmName.Name);

        if (!Directory.Exists(pulsarDir))
            pulsarDir = Path.Combine(baseDir, "Modern");

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
        string game2Dir = Folder.GetGame2();
        if (game2Dir is null)
        {
            Tools.ShowMessageBox(
                $"Error: {OldLauncher} not found!\n"
                    + "You can specify a custom location with \"-game2\""
            );
            Environment.Exit(1);
        }

        string modDir = Path.Combine(
            game2Dir,
            @"..\..\..\workshop\content",
            Steam.AppIdSe2.ToString()
        );

        Version se2Version = Game.GetGameVersion(game2Dir);
        if (se2Version is null) // Prevent NRE from Keen updates
            updater.ShowBitrotPrompt();

        ConfigManager.Init(game2Dir, modDir, se2Version);

        CoreConfig coreConfig = ConfigManager.Instance.Core;
        Version oldSe2Version = coreConfig.GameVersion;
        if (se2Version != oldSe2Version)
        {
            if (oldSe2Version is not null)
                Updater.GameUpdatePrompt(oldSe2Version, se2Version);

            coreConfig.GameVersion = se2Version;
            coreConfig.Save();
        }
    }

    private static void CheckCanStart()
    {
        string game2Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(game2Dir, OldLauncher);
        var launcher = new SharedLauncher(originalLoaderPath);

        if (!launcher.CanStart())
            Environment.Exit(1);
    }

    private static void SetupSteam()
    {
        SplashManager.Instance?.SetText("Starting Steam...");
        string game2Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += Steam.SteamworksResolver(game2Dir);
        Steam.Init(Steam.AppIdSe2);
    }

    private static void SetupPlugins(string baseDir)
    {
        SplashManager.Instance?.SetText("Getting Plugins...");

        var asmName = Assembly.GetExecutingAssembly().GetName();
        string dependencyDir = Path.Combine(baseDir, "Libraries", asmName.Name);

        string pulsarDir = ConfigManager.Instance.PulsarDir;
        string game2Dir = ConfigManager.Instance.GameDir;

        using (CompilerFactory compiler = new([game2Dir, dependencyDir], game2Dir, pulsarDir))
        {
            // The AppDomain must be created ASAP if running under Mono
            // as Mono does not isolate assemblies properly.
            if (!Tools.IsNative())
                compiler.Init();

            Tools.Init(new ExternalTools(), compiler);
            SharedLoader.Instance = new SharedLoader(GetCorePlugins());
        }

        Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Item2));
        if (preloader.HasPatches && !ConfigManager.Instance.SafeMode)
        {
            SplashManager.Instance?.SetText("Applying Preloaders...");
            string preloadDir = Path.Combine(pulsarDir, "Preloader");

            preloader.PreHooks();
            preloader.Patch(game2Dir, preloadDir);
            SetupGameResolver();
            preloader.PostHooks();
        }
        else
            SetupGameResolver();
    }

    private static string[] GetCorePlugins()
    {
        return [];
    }

    private static void SetupGameResolver()
    {
        string game2Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([game2Dir]);
    }

    private static ResolveEventHandler AssemblyResolver(string[] probeDirs)
    {
        return (sender, args) =>
        {
            string targetName = new AssemblyName(args.Name).Name;

            foreach (string probeDir in probeDirs)
            {
                string targetPath = Path.Combine(probeDir, targetName);

                if (File.Exists(targetPath + ".dll"))
                    return Assembly.LoadFrom(targetPath + ".dll");

                if (File.Exists(targetPath + ".exe"))
                    return Assembly.LoadFrom(targetPath + ".exe");
            }

            return null;
        };
    }

    private static void SetupGame(string[] args)
    {
        string game2Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(game2Dir, OldLauncher);

        LogFile.GameLog = new GameLog();

        // This is to prevent the game from loading the wrong System.Management assembly in the Game2 folder.
        Assembly.LoadFrom(
            Path.Combine(game2Dir, "runtimes\\win\\lib\\netcoreapp2.0\\System.Management.dll")
        );

        // This is to fix errors on game startup.
        // Game code uses GetEntryAssembly() and APP_CONTEXT_BASE_DIRECTORY AppContext variable,
        // which would point to the Pulsar folder instead.
        Assembly.SetEntryAssembly(
            AssemblyLoadContext.Default.LoadFromAssemblyPath(originalLoaderPath)
        );
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", game2Dir);

        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        new Harmony(assemblyName + ".Early").PatchCategory("Early");

        Game.RegisterPlugin(typeof(PluginLoader));

        SplashManager.Instance?.SetText("Launching Space Engineers 2...");
        if (Tools.IsNative())
            ProgressPollFactory().Start();

        Game.StartSpaceEngineers2(args);
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

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([
            Path.Combine(Environment.CurrentDirectory, "Libraries", "Modern"),
        ]);
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([Folder.GetGame2()]);

        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().UseReactiveUI();
    }
}
