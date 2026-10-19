using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Pulsar.Protocol.Interface;
using Pulsar.Shared.Arguments;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;
#if NETCOREAPP
using System.Formats.Tar;
#endif

namespace Pulsar.Shared;

public class Updater(string repoName)
{
    private const string UpdaterName = "Updater";
    private const string PulsarName = "Pulsar";
    private const string DebugArg = "-debug";

    private Version remotePulsarVer;

    public void TryUpdate()
    {
        Assembly entryAssembly = Assembly.GetEntryAssembly();
        Version localPulsarVer = entryAssembly.GetName().Version;

        bool preRelease = Flags.Current.UpdateType == UpdateType.Tester;

        if (
            Flags.Current.UpdateType == UpdateType.None
            || !GitHub.GetReleaseVersion(repoName, out remotePulsarVer, preRelease)
            || localPulsarVer >= remotePulsarVer
        )
            return;

        LogFile.WriteLine($"An update is available to {remotePulsarVer.ToString(3)}");

        if (!IsWritable(Path.GetDirectoryName(entryAssembly.Location)))
        {
            LogFile.Warn("Skipping update due to read-only Pulsar install!");
            return;
        }

        PromptResult result = ShowUpdatePrompt(localPulsarVer, remotePulsarVer);
        if (result == PromptResult.Yes)
            Update();
    }

    private static bool IsWritable(string directory)
    {
        string path = Path.Combine(directory, Path.GetRandomFileName());

        try
        {
            using (File.Create(path, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return false;
    }

    private static PromptResult ShowUpdatePrompt(Version localVer, Version remoteVer)
    {
        string prompt =
            $"An update is available for {PulsarName}:\n"
            + $"{localVer.ToString(3)} -> {remoteVer.ToString(3)}\n"
            + "Would you like to update now?";

        return Tools.ShowMessageBox(prompt, PromptButtons.YesNo, PromptIcon.Question);
    }

    public static void GameUpdatePrompt(Version oldVersion, Version newVersion, int fieldCount)
    {
        string change = (newVersion > oldVersion ? "up" : "down") + "graded";
        string prompt =
            $"Space Engineers has been {change}! "
            + $"({oldVersion.ToString(fieldCount)} -> {newVersion.ToString(fieldCount)})\n"
            + "All plugins must be rebuilt to target the new version.\n\n"
            + "Plugin build errors are NOT a Pulsar issue.\n"
            + "Authors of broken plugins have been notified: be patient.\n\n"
            + "If Pulsar causes instability report this on Discord or GitHub.\n"
            + "ONLY report an issue if:\n"
            + "- It does not happen without Pulsar loaded.\n"
            + "- It still happens with no plugins or mods loaded.\n"
            + "- It can be reproduced / you know what caused it.\n\n"
            + "Snapshots of the Plugin Hub are available if you choose to revert.\n"
            + "Do you wish to continue?";

        PromptResult result = Tools.ShowMessageBox(prompt, PromptButtons.YesNo, PromptIcon.Warning);

        if (result == PromptResult.No)
            Environment.Exit(0);

        GitHubPlugin.ClearGitHubCache();
        LocalFolderPlugin.ClearDevFolderCache();
    }

    public void ShowBitrotPrompt()
    {
        PromptButtons buttons;
        string message = "You have a broken Pulsar insallation!\n";

        if (Flags.Current.UpdateType == UpdateType.None)
        {
            message += "Please rebuild or manually redownload.";
            buttons = PromptButtons.Ok;
        }
        else
        {
            message += "Attempt to download the latest version?";
            buttons = PromptButtons.YesNo;
        }

        PromptResult result = Tools.ShowMessageBox(message, buttons, PromptIcon.Error);

        if (result == PromptResult.Yes)
            Update();

        Environment.Exit(1);
    }

    private static void ShowUpdateError()
    {
        string prompt =
            $"An error occurred while updating {PulsarName}!\n"
            + "Please check the log for more information!";

        Tools.ShowMessageBox(prompt, PromptButtons.Ok, PromptIcon.Error);
    }

    private void Update()
    {
        JObject json;
        try
        {
            json = GitHub.GetReleaseJson(repoName, $"v{remotePulsarVer.ToString(3)}");
        }
        catch (Exception e)
        {
            LogFile.Error("Error while fetching updater info: " + e);
            ShowUpdateError();
            return;
        }

        if (!TryGetPulsarPath(json, out string rPulsarPath))
        {
            ShowUpdateError();
            return;
        }

        string lPulsarPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        GitHubPlugin.ClearGitHubCache();
        LocalFolderPlugin.ClearDevFolderCache();

#if NETCOREAPP
        if (!Tools.IsWindows())
        {
            try
            {
                LinuxUpdater.Update(rPulsarPath, lPulsarPath);
            }
            catch (Exception e)
            {
                LogFile.Error("Error while updating: " + e);
                ShowUpdateError();
            }
            return;
        }
#endif

        if (!TryGetUpdaterInfo(json, out Version rUpdaterVer, out string rUpdaterPath))
        {
            ShowUpdateError();
            return;
        }

        string lUpdaterPath = Path.Combine(lPulsarPath, UpdaterName + ".exe");
        Version lUpdaterVer = GetLocalUpdaterVersion(lUpdaterPath);

        try
        {
            if (lUpdaterVer is null || lUpdaterVer < rUpdaterVer)
                DownloadUpdater(rUpdaterPath, lUpdaterPath);
        }
        catch (Exception e)
        {
            LogFile.Error($"Error while downloading {UpdaterName}: " + e);
            ShowUpdateError();
            return;
        }

        Tools.Interface.Dispose();
        StartUpdater(lUpdaterPath, rPulsarPath, lPulsarPath);
    }

    private static bool TryGetUpdaterInfo(
        JObject json,
        out Version remoteVer,
        out string remotePath
    )
    {
        remoteVer = null;
        remotePath = null;

        if (json["assets"] is not JArray assets)
            return false;

        foreach (JToken item in assets)
        {
            string name = item["name"].ToString();
            if (!name.Contains(UpdaterName))
                continue;

            string version = Tools.RemoveAll(name, [".exe", UpdaterName, "-v"]);
            remoteVer = new Version(version);
            remotePath = item["browser_download_url"].ToString();
            break;
        }

        if (remoteVer is null)
        {
            LogFile.Error($"Cannot find {UpdaterName} in assets.");
            return false;
        }

        return true;
    }

    private static bool TryGetPulsarPath(JObject json, out string remotePath)
    {
        remotePath = null;

        if (json["assets"] is not JArray assets)
            return false;

        // The first character of PulsarName is ignored (hence "ulsar")
        // Temporary workaround to prevent pre-v2.4 Pulsar versions from updating to a Linux
        // build (and bricking) by separating their releases based on first character's case.
        JToken asset = assets.FirstOrDefault(item =>
        {
            string name = item["name"].ToString();
            return name.Contains("ulsar") && name.Contains(Tools.RuntimeIdentifier);
        });

        remotePath = asset?["browser_download_url"]?.ToString();
        if (remotePath is not null)
            return true;

        LogFile.Error($"Updater cannot find {PulsarName} for {Tools.RuntimeIdentifier}.");
        return false;
    }

    private static Version GetLocalUpdaterVersion(string updaterPath)
    {
        if (!File.Exists(updaterPath))
            return null;

        try
        {
            return AssemblyName.GetAssemblyName(updaterPath).Version;
        }
        catch (Exception e)
        {
            // Older versions wrote it in place, so an interrupted download left a broken file
            LogFile.Warn($"Replacing unreadable {UpdaterName}: {e.Message}");
            return null;
        }
    }

    private static void DownloadUpdater(string remotePath, string localPath)
    {
        Uri uri = new(remotePath, UriKind.Absolute);
        string tempPath = localPath + ".download";

        try
        {
            NetworkClient.DownloadAsync(uri, tempPath).GetAwaiter().GetResult();
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }

        // A kill between these leaves no updater, which is downloaded again next time
        File.Delete(localPath);
        File.Move(tempPath, localPath);
    }

    private static void StartUpdater(string updaterPath, string remotePath, string localPath)
    {
        string caller = Process.GetCurrentProcess().MainModule.FileName;
        List<string> originalArgs = Tools.GetRestartArgs(caller);

        List<string> args = ["-caller", caller, "-remote", remotePath, "-local", localPath];
        args.AddRange(originalArgs);

        args.Remove(DebugArg);
        if (Debugger.IsAttached)
            args.Add(DebugArg);

        string cmdArgs = string.Join(" ", args.Select(a => $"\"{a}\""));

        ProcessStartInfo startInfo = new()
        {
            FileName = updaterPath,
            Arguments = cmdArgs,
            UseShellExecute = false,
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }
}

#if NETCOREAPP
file static class LinuxUpdater
{
    private const string Pulsar = "Pulsar";
    private const string DebugArg = "-debug";
    private const int MaxEntries = 15;

    // Kept inside the install folder, so they are on the same file system and writable
    private const string NewFolder = ".update-new";
    private const string OldFolder = ".update-old";

    private static readonly HashSet<string> Preserve =
    [
        "Legacy",
        "Interim",
        "Modern",
        "NuGet",
        NewFolder,
        OldFolder,
    ];
    private static readonly HashSet<string> Check = ["Interim.bin", "Modern.bin", "LICENSE"];

    public static void Update(string remote, string destination)
    {
        Uri uri = new(remote, UriKind.Absolute);
        using Stream stream = NetworkClient.GetStreamAsync(uri).GetAwaiter().GetResult();
        string caller = Process.GetCurrentProcess().MainModule.FileName;

        if (!Validate(destination))
            Environment.Exit(1);

        // Extract next to the old files first: failing here leaves the install untouched
        string newFolder = Path.Combine(destination, NewFolder);
        string oldFolder = Path.Combine(destination, OldFolder);
        DeleteFolder(newFolder);
        DeleteFolder(oldFolder);

        try
        {
            Directory.CreateDirectory(newFolder);
            using GZipStream gzip = new(stream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, newFolder, overwriteFiles: false);
        }
        catch
        {
            TryDeleteFolder(newFolder);
            throw;
        }

        Tools.Interface.Dispose();

        try
        {
            Swap(destination, newFolder, oldFolder);
        }
        catch (Exception e)
        {
            LogFile.Error("Cannot restore the previous Pulsar files: " + e);
            Environment.Exit(1);
        }

        TryDeleteFolder(newFolder);
        TryDeleteFolder(oldFolder);

        Launcher.ReleaseInstanceLock();
        Start(caller);
        Environment.Exit(0);
    }

    // Renames only, so there is nothing left to fail on a full disk.
    // On error the old files are put back and the old version restarts.
    private static void Swap(string destination, string newFolder, string oldFolder)
    {
        List<string> removed = [];
        List<string> added = [];

        try
        {
            Directory.CreateDirectory(oldFolder);

            foreach (string name in GetNames(destination).Where(n => !Preserve.Contains(n)))
            {
                Move(Path.Combine(destination, name), Path.Combine(oldFolder, name));
                removed.Add(name);
            }

            foreach (string name in GetNames(newFolder))
            {
                Move(Path.Combine(newFolder, name), Path.Combine(destination, name));
                added.Add(name);
            }
        }
        catch (Exception e)
        {
            LogFile.Error("Error while replacing Pulsar files, restoring them: " + e);

            foreach (string name in added)
                Move(Path.Combine(destination, name), Path.Combine(newFolder, name));

            foreach (string name in removed)
                Move(Path.Combine(oldFolder, name), Path.Combine(destination, name));
        }
    }

    private static List<string> GetNames(string folder) =>
        [.. Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName)];

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
            Directory.Move(source, destination);
        else
            File.Move(source, destination);
    }

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            DeleteFolder(folder);
        }
        catch (Exception e)
        {
            LogFile.Warn($"Cannot delete {folder}: {e.Message}");
        }
    }

    private static bool Validate(string folder)
    {
        if (!Directory.Exists(folder))
            return false;

        folder = Path.GetFullPath(folder);

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string defaultPath = Path.Combine(appData, Pulsar);

        if (folder == defaultPath)
            return true;

        bool isPulsarInstall = Check.All(name => File.Exists(Path.Combine(folder, name)));
        int entries = GetNames(folder).Count(n => n != NewFolder && n != OldFolder);
        if (isPulsarInstall && entries <= MaxEntries)
            return true;

        return ContinuePrompt(folder);
    }

    private static bool ContinuePrompt(string folder)
    {
        string message =
            "The installation folder could not be validated!\n"
            + "Is this your Pulsar install folder?\n"
            + "It WILL BE CLEANED if you update!\n\n"
            + folder;

        return Tools.ShowMessageBox(message, PromptButtons.YesNo, PromptIcon.Warning)
            == PromptResult.Yes;
    }

    private static void Start(string exe)
    {
        List<string> originalArgs = Tools.GetRestartArgs(exe);

        originalArgs.Remove(DebugArg);
        if (Debugger.IsAttached)
            originalArgs.Add(DebugArg);

        string cmdArgs = string.Join(" ", originalArgs.Select(a => $"\"{a}\""));

        ProcessStartInfo startInfo = new()
        {
            FileName = exe,
            Arguments = cmdArgs,
            UseShellExecute = false,
        };

        if (File.Exists(exe))
            Process.Start(startInfo);
    }
}
#endif
