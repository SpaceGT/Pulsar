using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Filesystem;
using Pulsar.Modern.Patch;
using Pulsar.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Pulsar.Modern.Launcher;

internal class GameLog : IGameLog
{
    public bool Exists()
    {
        string file = FileSystem.Instance.TempFiles.GetBasePath() + $"/Logs/{Log.Default.FileName}";
        return File.Exists(file) && file.EndsWith(".log");
    }

    public bool Open()
    {
        Log.Default.Flush();
        string file = FileSystem.Instance.TempFiles.GetBasePath() + $"/Logs/{Log.Default.FileName}";

        if (!File.Exists(file) || !file.EndsWith(".log"))
            return false;

        ProcessStartInfo psi = new(file)
        {
            UseShellExecute = true
        };
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
        Assembly.SetEntryAssembly(
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath)
        );
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", asmFolder);

        Environment.CurrentDirectory = asmFolder;
    }

    public static Version GetGameVersion(string game2Dir)
    {
        const string Assembly = "SpaceEngineers2.dll";

        var version = FileVersionInfo.GetVersionInfo(Path.Combine(game2Dir, Assembly));

        return new Version(version.FileVersion);
    }

    public static float GetLoadProgress()
    {
        // No native function in Space Engineers does this but we can estimate
        // FIXME: Does not work well with Preloaders or under Proton
        const float expectedGrowth = 1900f * 1024 * 1024;

        Process process = Process.GetCurrentProcess();
        process.Refresh();

        float ratio = process.PrivateMemorySize64 / expectedGrowth;

        return Math.Min(1f, Math.Max(0f, ratio));
    }

    public static void StartSpaceEngineers2(string[] args) => Keen.Game2.Program.Main(args);
}
