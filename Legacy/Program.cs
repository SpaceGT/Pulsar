using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Pulsar.Compiler;
using Pulsar.Interface;
using Pulsar.Legacy.Launcher;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Patch;
using Pulsar.Protocol.Interface;
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
    private const string StatsServer = "https://pluginstats.ferenczi.eu";

    static void Main(string[] args)
    {
#if NETCOREAPP

        string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string libraryDir = Path.Combine(baseDir, "Libraries", "Interim");
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([libraryDir, runtimeDir]);

        PulsarMain(args);
    }

    static void PulsarMain(string[] args)
    {
#endif
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        string baseDir = Path.GetDirectoryName(currentAssembly.Location);

        if (Flags.HelpRequested)
        {
            Flags.LogHelp();
            return;
        }

        string guiPath = Path.Combine(
            baseDir,
            "Libraries",
            "Interface",
            "Interface" + Tools.ExecutableExtension
        );

        using InterfaceClient interfaceClient = new(guiPath);
        Tools.EarlyInit(interfaceClient);

        if (SharedLauncher.IsOtherPulsarRunning())
        {
            string message = "Pulsar is already running!";
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            return;
        }

        if (Flags.ExternalDebug)
            Debugger.Launch();

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
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dataDir = Flags.UseHome ? Path.Combine(appData, "Pulsar") : baseDir;
        string pulsarDir = Path.Combine(dataDir, asmName.Name);

        if (!Directory.Exists(pulsarDir))
            pulsarDir = Path.Combine(dataDir, "Legacy");

        LogFile.Init(pulsarDir);
        LogFile.WriteLine($"Starting Pulsar v{asmName.Version.ToString(3)}");
        LogFile.WriteLine($"Flavour: {asmName.Name}");
        LogFile.WriteLine($"Platform: {Tools.Platform}");
        LogFile.WriteLine($"Runtime: {Tools.Runtime}");

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
            string message =
                $"{OldLauncher} not found!\nYou can specify a custom location with \"-bin64\"";
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            Environment.Exit(1);
        }

        string modDir = Path.Combine(
            bin64Dir,
            "..",
            "..",
            "..",
            "workshop",
            "content",
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
        Steam.Init(Steam.AppIdSe1);
    }

    private static void SetupPlugins(string baseDir)
    {
        SplashManager.Instance?.SetText("Getting Plugins...");

        var asmName = Assembly.GetExecutingAssembly().GetName();
        string dependencyDir = Path.Combine(baseDir, "Libraries", asmName.Name);
        string compilerPath = Path.Combine(
            baseDir,
            "Libraries",
            "Compiler",
            "Compiler" + Tools.ExecutableExtension
        );

        string pulsarDir = ConfigManager.Instance.PulsarDir;
        string bin64Dir = ConfigManager.Instance.GameDir;

        string[] runtimeDirs = CompilerFactory.GetRuntimeDirectories();

#if NETFRAMEWORK
        string wpfDir = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "WPF");
        string[] probeDirs = [.. runtimeDirs, wpfDir, bin64Dir, dependencyDir];
#else
        string[] probeDirs = [.. runtimeDirs, bin64Dir, dependencyDir];
#endif

        string[] references = [.. References.GetReferences(bin64Dir)];

        using (
            CompilerFactory compiler = new(
                compilerPath,
                references,
                probeDirs,
                LogFile.FilePath,
                Tools.GetCompilationSymbols(trusted: true)
            )
        )
        {
            Tools.Init(new ExternalTools(), compiler);
            SharedLoader.Instance = new SharedLoader(StatsServer, GetCorePlugins());
        }

        Preloader preloader = new(SharedLoader.Instance.Plugins.Select(x => x.Value));
        if (preloader.HasPatches)
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

        // Recompiled SpaceEngineers builds have built-in compatibility
        if (!Tools.GetFiles(bin64Dir, ["*.config"], []).Any())
            return [];

        return Tools.IsWindows() ? ["se-dotnet-compat"] : ["se-dotnet-compat", "se-linux-compat"];
#endif
    }

    private static void SetupGameResolver()
    {
        string bin64Dir = ConfigManager.Instance.GameDir;
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver([bin64Dir]);
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
        string bin64Dir = ConfigManager.Instance.GameDir;
        string originalLoaderPath = Path.Combine(bin64Dir, OldLauncher);
        Patch_PrepareCrashReport.SpaceEngineersPath = originalLoaderPath;

        LogFile.GameLog = new GameLog();

        Game.SetMainAssembly(originalLoaderPath);

        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        new Harmony(assemblyName + ".Early").PatchCategory("Early");
        Progress.Start(assemblyName + ".Progress");

        Game.SetupMyFakes();
        Game.ShowIntroVideo(Flags.GameIntroVideo);
        Game.RegisterPlugin(new PluginLoader());

        string[] symbols = Tools.GetCompilationSymbols(trusted: false);
        Game.AddCompilationSymbols(symbols);

        SplashManager.Instance?.SetText("Launching Space Engineers...");
        SplashManager.Instance?.SetBarValue(0);
        Game.StartSpaceEngineers(args);
    }
}
