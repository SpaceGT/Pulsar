using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using Pulsar.Shared;

namespace Pulsar.Modern.Launcher;

internal class Folder
{
    private const string registryKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {0}";
    private const string registryName = "InstallLocation";

    private const string se2Launcher = "SpaceEngineers2.exe";
    private static readonly HashSet<string> se2Files =
    [
        se2Launcher,
        "Game2.Game.dll",
        "VRage.Core.dll",
    ];

    public static string GetGame2() =>
        FromOverride() ?? FromSteamArgs() ?? FromSteamFiles() ?? FromRegistry();

    private static bool IsGame2(string path)
    {
        if (!Directory.Exists(path))
            return false;

        foreach (string file in se2Files)
            if (!File.Exists(Path.Combine(path, file)))
                return false;

        return true;
    }

    private static string TryConvertUnix(string path)
    {
        // We assume paths in this context refer to the Unix system root
        // rather then the current root of the Proton prefix.
        // Windows can handle forward slashes in paths so all we need to
        // do is point it to where the system root is mounted under.

        if (!Tools.IsNative() && path.StartsWith('/'))
            return "Z:" + path;
        return path;
    }

    private static string FromRegistry()
    {
        using var baseKey = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            RegistryView.Registry64
        );

        using var key = baseKey.OpenSubKey(string.Format(registryKey, Steam.AppIdSe2));
        if (key is null)
            return null;

        var installLocation = key.GetValue(registryName) as string;
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        string path = Path.Combine(installLocation, "Game2");
        if (!IsGame2(path))
            return null;

        return path;
    }

    private static string FromOverride()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.FindIndex(
            args,
            arg => arg.Equals("-game2", StringComparison.OrdinalIgnoreCase)
        );

        if (index < 0 || index >= args.Length - 1)
            return null;

        string path = args[index + 1];
        if (!Path.IsPathRooted(path))
        {
            string currentPath = Assembly.GetExecutingAssembly().Location;
            string currentDir = Path.GetDirectoryName(currentPath);
            path = Path.Combine(currentDir, path);
        }
        else
            path = TryConvertUnix(path);

        if (!IsGame2(path))
            return null;

        return Path.GetFullPath(path);
    }

    private static string FromSteamArgs()
    {
        // The original command (which inlcudes a path to seLauncher) will
        // be present if substituted in with Steam's %command% argument.

        IEnumerable<string> sePaths = Environment
            .GetCommandLineArgs()
            .Where(arg => arg.Contains("Game2") && arg.Contains(se2Launcher))
            .Select(TryConvertUnix)
            .Select(Path.GetDirectoryName);

        foreach (string path in sePaths)
            if (IsGame2(path))
                return path;

        return null;
    }

    private static string FromSteamFiles()
    {
        // VDF files within Proton prefixes are unreliable.
        if (!Tools.IsNative())
            return null;

        string steamPath = Steam.GetSteamPath();

        if (steamPath is null)
            return null;

        string libraryPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        VProperty libraries = VdfConvert.Deserialize(File.ReadAllText(libraryPath));

        foreach (var library in libraries.Value.Children<VProperty>())
        {
            var data = (VObject)library.Value;
            var apps = (VObject)data["apps"];

            if (!apps.ContainsKey(Steam.AppIdSe2.ToString()))
                continue;

            string targetPath = data.Value<string>("path");
            string game2 = Path.Combine(
                targetPath,
                "steamapps",
                "common",
                "SpaceEngineers2",
                "Game2"
            );

            if (IsGame2(game2))
                return game2;
        }

        return null;
    }
}
