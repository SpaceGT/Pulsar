using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace Pulsar.Updater;

static class Program
{
    private const string DebugArg = "-debug";

    static void Main()
    {
        Application.EnableVisualStyles();

        if (Tools.HasCommandArg(DebugArg))
            Debugger.Launch();

        string? caller = Tools.GetCommandArg("-caller");
        string? remote = Tools.GetCommandArg("-remote");
        string? local = Tools.GetCommandArg("-local");

        if (caller is null || remote is null || local is null)
        {
            ShowInfo();
            return;
        }

        Uri uri = new(remote, UriKind.Absolute);
        using Stream stream = Network.GetStream(uri);
        using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        Writer.Update(zip, local);
        Start(caller);
    }

    private static void ShowInfo()
    {
        string caption = "Pulsar Updater";
        string message =
            "This program used by Pulsar when updating and should not be ran directly.\n"
            + "You are free to delete it - a new copy will be fetched when required.";
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void Start(string exe)
    {
        List<string> originalArgs = [.. Environment.GetCommandLineArgs().Skip(7)];

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

        Process.Start(startInfo);
    }
}
