using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using Pulsar.Shared;
using Pulsar.Shared.Vdf.Steam;

namespace Pulsar.Legacy.Launcher;

internal class Folder
{
    private const string registryKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {0}";
    private const string registryName = "InstallLocation";
    private static readonly HashSet<string> seFiles =
    [
        "SpaceEngineers.exe",
        "SpaceEngineers.Game.dll",
        "VRage.dll",
        "Sandbox.Game.dll",
        "ProtoBuf.Net.dll",
    ];

    public static string GetBin64() => FromArguments() ?? FromRegistry() ?? FromSteamLibraryFoldersVdf();

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

    private static string FromArguments()
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

    private static string FromSteamLibraryFoldersVdf()
    {
        string steamInstallPath = Steam.GetSteamPath();
        if (steamInstallPath is null)
            return null;

        string libaryFoldersVdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libaryFoldersVdfPath))
            return null;

        try
        {
            using var libraryFolderVdfReader = File.OpenText(libaryFoldersVdfPath);
            VProperty libraryFoldersData = VdfConvert.Deserialize(libraryFolderVdfReader);
            var libraryFolders = libraryFoldersData.Value.Select(i => TryParseLibraryFolder(((VProperty)i).Value));

            if (libraryFolders is null)
                return null;

            foreach (LibraryFolder? folder in libraryFolders)
            {
                if (folder?.path is null || (folder?.apps?.Count ?? 0) is 0)
                    continue;

                foreach (ulong appId in folder!.apps.Keys)
                {
                    string bin64Location = Path.Combine(folder.path, "steamapps", "common", "SpaceEngineers", "Bin64");
                    if (appId == 244850 && ValidateBin64Path(bin64Location))
                    {
                        return bin64Location;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;

        static LibraryFolder? TryParseLibraryFolder(VToken token)
        {
            try
            {
                return new LibraryFolder
                {
                    path = token.Value<string>("path") ?? null,
                    label = token.Value<string>("label") ?? null,
                    contentid = token.Value<ulong>("contentid"),
                    totalsize = token.Value<ulong>("totalsize"),
                    update_clean_bytes_tally = token.Value<ulong>("update_clean_bytes_tally"),
                    time_last_update_verified = token.Value<ulong>("time_last_update_verified"),
                    apps = token["apps"]?.Children<VProperty>()?.ToDictionary(k => ulong.Parse(k.Key), v => ulong.Parse(v.Value.ToString())),
                };
            }
            catch
            {
                return null;
            }
        }

        static bool ValidateBin64Path(string path)
        {
            if (!Path.IsPathRooted(path))
                return false;

            if (!Directory.Exists(path))
                return false;

            string gameExePath = Path.Combine(path, "SpaceEngineers.exe");
            if (!File.Exists(gameExePath))
                return false;

            return true;
        }
    }
}
