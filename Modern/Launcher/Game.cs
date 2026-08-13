using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Keen.VRage.Core;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Utils;
using Pulsar.Modern.Patch;
using Pulsar.Shared;

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

    public static void SetMainAssembly(string assemblyPath)
    {
        string asmFolder = Path.GetDirectoryName(assemblyPath);

        // This is to fix errors on game startup.
        // Game code uses GetEntryAssembly() and APP_CONTEXT_BASE_DIRECTORY AppContext variable,
        // which would point to the Pulsar folder instead.
        Assembly.SetEntryAssembly(AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", asmFolder);

        Environment.CurrentDirectory = asmFolder;
    }

    public static Version GetGameVersion(string game2Dir)
    {
        const string Assembly = "SpaceEngineers2.dll";

        var version = FileVersionInfo.GetVersionInfo(Path.Combine(game2Dir, Assembly));

        return new Version(version.FileVersion);
    }

    public static void StartSpaceEngineers2(string[] args)
    {
        // Prefer native SE2 arguments for Flag implementation
        if (Flags.ContinueGame)
            args = [.. args, "-startLast"];
        if (Flags.MultiInstance)
            args = [.. args, "-allowMultiple"];

        Keen.Game2.Program.Main(args);
    }
}
