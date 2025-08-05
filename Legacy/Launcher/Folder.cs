using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using Pulsar.Shared;

namespace Pulsar.Legacy.Launcher
{
    internal class Folder
    {
        private const string steamInstallDir =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {0}";
        private static readonly HashSet<string> seFiles =
        [
            "SpaceEngineers.exe",
            "VRage.dll",
            "Sandbox.Game.dll",
            "ProtoBuf.Net.dll",
        ];

        public static string GetBin64() => FromArguments() ?? FromCurrentDir() ?? FromRegistry();

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

            using var key = baseKey.OpenSubKey(string.Format(steamInstallDir, Steam.AppId));
            if (key == null)
                return null;

            var installLocation = key.GetValue("InstallLocation") as string;
            if (string.IsNullOrWhiteSpace(installLocation))
                return null;

            string path = Path.Combine(installLocation, "Bin64");
            if (!IsBin64(path))
                return null;

            return path;
        }

        private static string FromCurrentDir()
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string currentDir = Path.GetDirectoryName(Path.GetFullPath(currentAssembly.Location));
            string path = Path.Combine(currentDir, "..");

            if (!IsBin64(path))
                return null;

            return path;
        }

        private static string FromArguments()
        {
            string rawPath = Tools.GetCommandArg("-bin64");
            if (rawPath == null)
                return null;

            string path = Path.GetFullPath(rawPath);
            if (!IsBin64(path))
                return null;

            return path;
        }
    }
}
