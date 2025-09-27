using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;

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

        bool preRelease = Flags.UpdateType == UpdateType.Tester;

        if (
            Flags.UpdateType != UpdateType.None
            && GitHub.GetReleaseVersion(repoName, out remotePulsarVer, preRelease)
            && localPulsarVer < remotePulsarVer
        )
        {
            LogFile.WriteLine($"An update is available to {remotePulsarVer.ToString(3)}");

            DialogResult result = ShowUpdatePrompt(localPulsarVer, remotePulsarVer);
            if (result == DialogResult.Yes)
                Update();
            else if (result == DialogResult.Cancel)
                Environment.Exit(0);
        }
    }

    private static DialogResult ShowUpdatePrompt(Version localVer, Version remoteVer)
    {
        string prompt =
            $"An update is available for {PulsarName}:\n"
            + $"{localVer.ToString(3)} -> {remoteVer.ToString(3)}\n"
            + "Would you like to update now?";

        return Tools.ShowMessageBox(prompt, MessageBoxButtons.YesNoCancel);
    }

    public static void GameUpdatePrompt(Version oldVersion, Version newVersion)
    {
        string change = (newVersion > oldVersion ? "up" : "down") + "graded";
        string prompt =
            $"Space Engineers has been {change}! "
            + $"({oldVersion.ToString(3)} -> {newVersion.ToString(3)})\n"
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

        DialogResult result = Tools.ShowMessageBox(prompt, MessageBoxButtons.YesNo);

        if (result == DialogResult.No)
            Environment.Exit(0);

        GitHubPlugin.ClearGitHubCache();
    }

    public void ShowBitrotPrompt()
    {
        MessageBoxButtons buttons;
        string message = "You have a broken Pulsar insallation!\n";

        if (Flags.UpdateType == UpdateType.None)
        {
            message += "Please rebuild or manually redownload.";
            buttons = MessageBoxButtons.OK;
        }
        else
        {
            message += "Attempt to download the latest version?";
            buttons = MessageBoxButtons.YesNo;
        }

        DialogResult result = Tools.ShowMessageBox(message, buttons);

        if (result == DialogResult.Yes)
            Update();

        Environment.Exit(1);
    }

    private static void ShowUpdateError()
    {
        string prompt =
            $"An error occurred while updating {PulsarName}!\n"
            + "Please check the log for more information!";

        Tools.ShowMessageBox(prompt, MessageBoxButtons.OK);
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

        if (
            !TryGetUpdaterInfo(json, out Version rUpdaterVer, out string rUpdaterPath)
            || !TryGetPulsarPath(json, out string rPulsarPath)
        )
        {
            ShowUpdateError();
            return;
        }

        string lPulsarPath = Path.Combine(ConfigManager.Instance.PulsarDir, "..");
        string lUpdaterPath = Path.Combine(lPulsarPath, UpdaterName + ".exe");
        Version lUpdaterVer = GetLocalUpdaterVersion(lUpdaterPath);

        if (lUpdaterVer is null || lUpdaterVer < rUpdaterVer)
            DownloadUpdater(rUpdaterPath, lUpdaterPath);

        GitHubPlugin.ClearGitHubCache();
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

        foreach (JToken item in assets)
        {
            string name = item["name"].ToString();
            if (!name.Contains(PulsarName))
                continue;

            remotePath = item["browser_download_url"].ToString();
            break;
        }

        if (remotePath is null)
        {
            LogFile.Error($"Cannot find {PulsarName} in assets.");
            return false;
        }

        return true;
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
        using var stream = GitHub.GetStream(uri);
        using var file = File.Create(localPath);
        stream.CopyTo(file);
    }

    private static void StartUpdater(string updaterPath, string remotePath, string localPath)
    {
        string caller = Assembly.GetEntryAssembly().Location;

        List<string> args = ["-caller", caller, "-remote", remotePath, "-local", localPath];
        args.AddRange(Environment.GetCommandLineArgs().Skip(1));

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
