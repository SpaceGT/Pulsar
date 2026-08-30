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
        else if (result == PromptResult.Cancel)
            Environment.Exit(0);
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

        // Unattended machines skip the update and keep launching; the Cancel
        // fallback would exit the process instead.
        return Tools.ShowMessageBox(
            prompt,
            PromptButtons.YesNoCancel,
            PromptIcon.Question,
            PromptResult.No
        );
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

        // Unattended machines must continue: any other fallback would exit
        // on every launch after a game update, boot-looping hosts that
        // restart the process automatically.
        PromptResult result = Tools.ShowMessageBox(
            prompt,
            PromptButtons.YesNo,
            PromptIcon.Warning,
            PromptResult.Yes
        );

        if (result != PromptResult.Yes)
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
            LinuxUpdater.Update(rPulsarPath, lPulsarPath);
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

        if (lUpdaterVer is null || lUpdaterVer < rUpdaterVer)
            DownloadUpdater(rUpdaterPath, lUpdaterPath);

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

        AssemblyName name = AssemblyName.GetAssemblyName(updaterPath);
        return name.Version;
    }

    private static void DownloadUpdater(string remotePath, string localPath)
    {
        Uri uri = new(remotePath, UriKind.Absolute);
        using Stream input = NetworkClient.GetStreamAsync(uri).GetAwaiter().GetResult();
        using FileStream output = File.Create(localPath);
        input.CopyTo(output);
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

    private static readonly HashSet<string> Preserve = ["Legacy", "Interim", "Modern", "NuGet"];
    private static readonly HashSet<string> Check = ["Interim.bin", "Modern.bin", "LICENSE"];

    public static void Update(string remote, string destination)
    {
        Uri uri = new(remote, UriKind.Absolute);
        using Stream stream = NetworkClient.GetStreamAsync(uri).GetAwaiter().GetResult();
        using GZipStream gzip = new(stream, CompressionMode.Decompress);
        string caller = Process.GetCurrentProcess().MainModule.FileName;

        if (!Validate(destination))
            Environment.Exit(1);

        Tools.Interface.Dispose();
        CleanFolder(destination, Preserve);
        TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);

        Launcher.ReleaseInstanceLock();
        Start(caller);
        Environment.Exit(0);
    }

    private static void CleanFolder(string folder, HashSet<string> exclude)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
            if (!exclude.Contains(Path.GetFileName(file)))
                File.Delete(file);

        foreach (string dir in Directory.EnumerateDirectories(folder))
            if (!exclude.Contains(Path.GetFileName(dir)))
                Directory.Delete(dir, recursive: true);
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
        bool hasOtherFiles = Directory.GetFileSystemEntries(folder).Length > MaxEntries;
        if (isPulsarInstall && !hasOtherFiles)
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
