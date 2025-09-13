using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;

namespace Pulsar.Shared;

public class Updater(string repoName, bool seMismatch, bool noUpdate)
{
    // Stub file for a simple updater and loader for the Updater project
    // Launcher is responsible for the initial version checking

    private const string ReleaseInfo = "https://api.github.com/repos/{0}/releases/latest";
    private const string UpdaterName = "Updater";
    private const string PulsarName = "Pulsar";
    private const string DebugArg = "-debug";

    public bool ShouldUpdate()
    {
        Assembly entryAssembly = Assembly.GetEntryAssembly();
        Version localPulsarVer = entryAssembly.GetName().Version;

        if (
            !noUpdate
            && GitHub.GetRepoVersion(repoName, out Version remotePulsarVer)
            && localPulsarVer < remotePulsarVer
        )
        {
            LogFile.WriteLine("An update is available to " + remotePulsarVer);

            DialogResult result = ShowUpdatePrompt(localPulsarVer, remotePulsarVer);
            if (result == DialogResult.Yes)
                return true;
            else if (result == DialogResult.Cancel)
                Environment.Exit(0);
        }

        if (seMismatch)
            ShowMismatchWarning();

        return false;
    }

    private static DialogResult ShowUpdatePrompt(Version localVer, Version remoteVer)
    {
        StringBuilder prompt = new();
        prompt.Append($"An update is available for {PulsarName}:").AppendLine();
        prompt.Append(localVer.ToString(3)).Append(" -> ").Append(remoteVer).AppendLine();
        prompt.Append("Would you like to update now?");

        DialogResult result = Tools.ShowMessageBox(
            prompt.ToString(),
            MessageBoxButtons.YesNoCancel
        );

        return result;
    }

    private void ShowMismatchWarning()
    {
        StringBuilder message = new();
        message.Append("Space Engineers has been updated!\n");
        if (noUpdate)
            message.Append($"Please rebuild {PulsarName} for the current version.\n\n");
        else
            message.Append($"Please wait for {PulsarName} to update!\n\n");
        message.Append("Do you want to launch anyway? (expect instability)");

        DialogResult result = Tools.ShowMessageBox(message.ToString(), MessageBoxButtons.YesNo);

        if (result == DialogResult.No)
            Environment.Exit(0);
    }

    private void ShowUpdateError()
    {
        StringBuilder prompt = new();
        prompt.Append("An error occurred while updating!").AppendLine();
        prompt.Append("Please check the log for more information!");

        Tools.ShowMessageBox(prompt.ToString(), MessageBoxButtons.OK);

        if (seMismatch)
            ShowMismatchWarning();
    }

    public void Update()
    {
        JObject json;
        try
        {
            json = GitHub.GetRepoJson(string.Format(ReleaseInfo, repoName));
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
