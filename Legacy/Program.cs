using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pulsar.Legacy.Compiler;
using Pulsar.Legacy.Launcher;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Patch;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using SharedLauncher = Pulsar.Shared.Launcher;
using SharedLoader = Pulsar.Shared.Loader;
#if NETCOREAPP
using System.Runtime.InteropServices;
#endif

namespace Pulsar.Legacy;

static class Program
{
    class ExternalTools : IExternalTools
    {
        public void OnMainThread(Action action) => Game.RunOnGameThread(action);
    }

    private const string PulsarRepo = "SpaceGT/Pulsar";
    private const string OldLauncher = "SpaceEngineers.exe";
    private const string StatsServer = "https://pluginstats.ferenczi.eu";

    static void Main(string[] args)
    {
#if NETCOREAPP

        string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string libraryDir = Path.Combine(baseDir, "Libraries", "Interim");
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([libraryDir, runtimeDir]);
        AppDomain.CurrentDomain.AssemblyResolve += Steam.SteamworksResolver(baseDir);
        SetupSteamNativeResolver(baseDir);
        SetupSdlNativeResolver(baseDir);

        PulsarMain(args);
    }

    static void PulsarMain(string[] args)
    {
#endif
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
        CheckCanStart(updater);
        SetupSteam();
        SetupPlugins(baseDir);
        SetupGame(args);
    }

    private static void SetupCoreData(string baseDir)
    {
        Environment.CurrentDirectory = baseDir;

        var asmName = Assembly.GetExecutingAssembly().GetName();
        string pulsarDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "Pulsar");

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
            Steam.AppIdSe1.ToString()
        );

        Version seVersion = Game.GetGameVersion(bin64Dir);
        if (seVersion is null) // Prevent NRE from Keen updates
            updater.ShowBitrotPrompt();

        RemoteHubConfig[] defaultHubs =
        [
            new RemoteHubConfig()
            {
                Name = "PluginHub",
                Repo = "StarCpt/PluginHub",
                Branch = "main",
                Enabled = true,
                Hash = null,
                LastCheck = null,
                Trusted = true,
            },
        ];

        ConfigManager.Init(bin64Dir, modDir, seVersion, defaultHubs);

        CoreConfig coreConfig = ConfigManager.Instance.Core;
        Version oldSeVersion = coreConfig.GameVersion;
        if (seVersion != oldSeVersion)
        {
            if (oldSeVersion is not null)
                Updater.GameUpdatePrompt(oldSeVersion, seVersion, 3);

            coreConfig.GameVersion = seVersion;
            coreConfig.Save();
        }
    }

    private static void CheckCanStart(Updater updater)
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(bin64Dir, OldLauncher);
        var launcher = new SharedLauncher(originalLoaderPath);

#if NETFRAMEWORK
        if (!launcher.VerifyConfig())
            updater.ShowBitrotPrompt();
#endif

        if (!launcher.CanStart())
            Environment.Exit(1);
    }

    private static void SetupSteam()
    {
        SplashManager.Instance?.SetText("Starting Steam...");
        string bin64Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += Steam.SteamworksResolver(bin64Dir);
        Steam.Init(Steam.AppIdSe1);
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
            SharedLoader.Instance = new SharedLoader(StatsServer, GetCorePlugins());
        }

        Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Item2));
        if (preloader.HasPatches && !ConfigManager.Instance.SafeMode)
        {
            SplashManager.Instance?.SetText("Applying Preloaders...");
            string preloadDir = Path.Combine(pulsarDir, "Preloader");

            preloader.PreHooks();
            preloader.Patch(bin64Dir, preloadDir);
            SetupGameResolver();
            preloader.PostHooks();
        }
        else
            SetupGameResolver();
    }

    private static string[] GetCorePlugins()
    {
#if NETFRAMEWORK
        return [];
#else
        string bin64Dir = ConfigManager.Instance.GameDir;
        bool isGameFramework = Tools.GetFiles(bin64Dir, ["*.config"], []).Any();
        return isGameFramework ? ["se-dotnet-compat"] : [];
#endif
    }

    private static void SetupGameResolver()
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([bin64Dir]);
    }

#if NETCOREAPP
    private static void SetupSteamNativeResolver(string baseDir)
    {
        Assembly steamworksAssembly = Assembly.Load("Steamworks.NET");
        NativeLibrary.SetDllImportResolver(steamworksAssembly, (name, assembly, searchPath) =>
        {
            if (name == "steam_api64")
            {
                string path = Path.Combine(baseDir, "libsteam_api.so");
                if (NativeLibrary.TryLoad(path, out IntPtr handle))
                    return handle;
            }
            return IntPtr.Zero;
        });
    }

    // Maps the splash's [DllImport("SDL3")] / [DllImport("SDL3_ttf")] to the
    // versioned sonames most Linux distros ship (libSDL3.so.0 /
    // libSDL3_ttf.so.0) and probes Pulsar's bundled Libraries dir plus the
    // common Steam runtime locations. Without this, .NET only tries
    // libSDL3_ttf.so (no version suffix), which most distros don't install.
    private static void SetupSdlNativeResolver(string baseDir)
    {
        Assembly sharedAssembly = typeof(Pulsar.Shared.Splash.SplashManager).Assembly;
        string home = Environment.GetEnvironmentVariable("HOME") ?? "";
        string[] extraSearchDirs =
        [
            Path.Combine(baseDir, "..", "Libraries"), // Pulsar/Libraries (sibling of Bin)
            Path.Combine(baseDir, "Libraries"),       // fallback
            Path.Combine(home, ".steam", "debian-installation", "ubuntu12_64"),
            Path.Combine(home, ".steam", "debian-installation", "steamrt64"),
            Path.Combine(home, ".steam", "steam", "ubuntu12_64"),
            Path.Combine(home, ".steam", "steam", "steamrt64"),
            Path.Combine(home, ".local", "share", "Steam", "ubuntu12_64"),
        ];

        NativeLibrary.SetDllImportResolver(sharedAssembly, (name, assembly, searchPath) =>
        {
            string[] candidates;
            switch (name)
            {
                case "SDL3":
                    candidates = ["libSDL3.so", "libSDL3.so.0"];
                    break;
                case "SDL3_ttf":
                    candidates = ["libSDL3_ttf.so", "libSDL3_ttf.so.0"];
                    break;
                default:
                    return IntPtr.Zero;
            }

            // First try the default loader path (LD_LIBRARY_PATH + ldconfig).
            foreach (string c in candidates)
                if (NativeLibrary.TryLoad(c, assembly, searchPath, out IntPtr handle))
                    return handle;

            // Then try absolute paths in known fallback locations.
            foreach (string dir in extraSearchDirs)
                foreach (string c in candidates)
                {
                    string full = Path.Combine(dir, c);
                    if (NativeLibrary.TryLoad(full, out IntPtr handle))
                        return handle;
                }

            return IntPtr.Zero;
        });
    }
#endif

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
        string bin64Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(bin64Dir, OldLauncher);
        Patch_PrepareCrashReport.SpaceEngineersPath = originalLoaderPath;

        LogFile.GameLog = new GameLog();

        Game.SetMainAssembly(originalLoaderPath);

        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        new Harmony(assemblyName + ".Early").PatchCategory("Early");

        Game.SetupMyFakes();
        Game.ShowIntroVideo(Flags.GameIntroVideo);
        Game.RegisterPlugin(new PluginLoader());

#if NETCOREAPP
        Game.AddCompilationSymbols("NETCOREAPP");
#endif

        SplashManager.Instance?.SetText("Launching Space Engineers...");

        // Close the Pulsar splash and fully tear down SDL before SE starts.
        // SplashManager.Delete() blocks until the render thread joins and
        // SDL_Quit has run, so by the time StartSpaceEngineers is called the
        // SDL/X11 connection is fully released and the se-linux-compat plugin
        // can call SDL_Init again cleanly.
        SplashManager.Instance?.Delete();

        Game.StartSpaceEngineers(args);
    }
}
