using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using Pulsar.Shared;

namespace Pulsar.Legacy.Launcher;

internal class Folder
{
    private const string registryKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {0}";
    private const string registryName = "InstallLocation";

    private const string seLauncher = "SpaceEngineers.exe";
    private static readonly HashSet<string> seFiles =
    [
        seLauncher,
        "SpaceEngineers.Game.dll",
        "VRage.dll",
        "Sandbox.Game.dll",
        "ProtoBuf.Net.dll",
    ];

    public static string GetBin64() =>
        FromOverride() ?? FromSteamArgs() ?? FromSteamFiles() ?? FromRegistry();

    private static bool IsBin64(string path)
    {
        if (!Directory.Exists(path))
            return false;

        foreach (string file in seFiles)
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

        if (!Tools.IsNative() && path.StartsWith("/"))
            return "Z:" + path;
        return path;
    }

    private static string FromRegistry()
    {
        using var baseKey = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            RegistryView.Registry64
        );

        using var key = baseKey.OpenSubKey(string.Format(registryKey, Steam.AppId));
        if (key is null)
            return null;

        var installLocation = key.GetValue(registryName) as string;
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        string path = Path.Combine(installLocation, "Bin64");
        if (!IsBin64(path))
            return null;

        return path;
    }

    private static string FromOverride()
    {
        string path = Tools.GetCommandArg("-bin64");
        if (path is null)
            return null;

        if (!Path.IsPathRooted(path))
        {
            string currentPath = Assembly.GetExecutingAssembly().Location;
            string currentDir = Path.GetDirectoryName(currentPath);
            path = Path.Combine(currentDir, path);
        }
        else
            path = TryConvertUnix(path);

        if (!IsBin64(path))
            return null;

        return Path.GetFullPath(path);
    }

    private static string FromSteamArgs()
    {
        // The original command (which inlcudes a path to seLauncher) will
        // be present if substituted in with Steam's %command% argument.

        IEnumerable<string> sePaths = Environment
            .GetCommandLineArgs()
            .Where(arg => arg.Contains("Bin64") && arg.Contains(seLauncher))
            .Select(TryConvertUnix)
            .Select(Path.GetDirectoryName);

        foreach (string path in sePaths)
            if (IsBin64(path))
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

            if (!apps.ContainsKey(Steam.AppId.ToString()))
                continue;

            string targetPath = data.Value<string>("path");
            string bin64 = Path.Combine(
                targetPath,
                "steamapps",
                "common",
                "SpaceEngineers",
                "Bin64"
            );

            if (IsBin64(bin64))
                return bin64;
        }

        return null;
    }
}
