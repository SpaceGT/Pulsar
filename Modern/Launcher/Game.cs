using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Keen.VRage.Core;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Utils;
using Pulsar.Modern.Patch;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Launcher;

internal class GameLog : IGameLog
{
    public bool Exists()
    {
        string file =
            Singleton<VRageCore>.Instance.AppDataPath + $"/Temp/Logs/{Log.Default.FileName}";
        return File.Exists(file) && file.EndsWith(".log");
    }

    public bool Open()
    {
        Log.Default.Flush();
        string file =
            Singleton<VRageCore>.Instance.AppDataPath + $"/Temp/Logs/{Log.Default.FileName}";

        if (!File.Exists(file) || !file.EndsWith(".log"))
            return false;

        ProcessStartInfo psi = new(file) { UseShellExecute = true };
        Process.Start(psi);

        return true;
    }

    public void Write(string line) => Log.Default.WriteLine($"[Pulsar]: {line}");
}

internal static class Game
{
    public static void RegisterPlugin(Type plugin)
    {
        Patch_LoadPlugin.PluginsToLoad.Add(plugin);
    }

    public static void SetMainAssembly(string assemblyPath, ref string[] args)
    {
        string asmFolder = Path.GetDirectoryName(assemblyPath);
        string gameRoot = Directory.GetParent(ConfigManager.Instance.GameDir).FullName;
        string vanillaProject = Path.Combine(gameRoot, "GameData", "Vanilla", "Vanilla.vrgproj");

        Assembly.SetEntryAssembly(AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", asmFolder);

        if (!args.Any(arg => arg.StartsWith("-projectPaths:")))
            args = [.. args, $"-projectPaths:{vanillaProject}"];
        else
            LogFile.Warn("Unset '-projectPaths' or add 'Vanilla.vrgproj' for full preloaders!");

        Environment.CurrentDirectory = asmFolder;
    }

    public static Version GetGameVersion(string game2Dir)
    {
        const string Assembly = "SpaceEngineers2.dll";

        var version = FileVersionInfo.GetVersionInfo(Path.Combine(game2Dir, Assembly));

        return new Version(version.FileVersion);
    }

    [SuppressMessage("Interoperability", "CA1416", Justification = "Handled by SE Linux port")]
    public static void StartSpaceEngineers2(string[] args)
    {
        // Prefer native SE2 arguments for Flag implementation
        if (Flags.Current.ContinueGame)
            args = [.. args, "-startLast"];
        if (Flags.Current.MultiInstance)
            args = [.. args, "-allowMultiple"];
        if (Flags.Current.SplashType != SplashType.Native)
            args = [.. args, "-nosplash"];

        Keen.Game2.Program.Main(args);
    }
}
