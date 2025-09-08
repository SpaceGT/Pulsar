using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

    public static string GetBin64() => FromOverride() ?? FromSteamArgs() ?? FromRegistry();

    private static bool IsBin64(string path)
    {
        if (!Directory.Exists(path))
            return false;

        foreach (string file in seFiles)
            if (!File.Exists(Path.Combine(path, file)))
                return false;

        return true;
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
            .Where(arg => arg.Contains(@"Bin64\" + seLauncher))
            .Select(Path.GetDirectoryName);

        foreach (string path in sePaths)
            if (IsBin64(path))
                return path;

        return null;
    }
}
