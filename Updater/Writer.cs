using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Pulsar.Updater;

internal static class Writer
{
    private const string Pulsar = "Pulsar";
    private const int MaxFiles = 15;

    private static readonly HashSet<string> Preserve = ["Legacy", "Modern"];
    private static readonly HashSet<string> Check = ["Legacy.exe", "Modern.exe", "LICENSE"];

    public static void Update(ZipArchive source, string destination)
    {
        if (!Validate(destination))
            Environment.Exit(1);

        CleanFolder(destination, Preserve);
        source.ExtractToDirectory(destination);
    }

    private static void CleanFolder(string folder, HashSet<string> exclude)
    {
        string updater = Assembly.GetExecutingAssembly().Location;
        if (IsUpdaterFolder(updater, folder))
            exclude.Add(Path.GetFileName(updater));

        foreach (string file in Directory.EnumerateFiles(folder))
            if (!exclude.Contains(Path.GetFileName(file)))
                File.Delete(file);

        foreach (string dir in Directory.EnumerateDirectories(folder))
            if (!exclude.Contains(Path.GetFileName(dir)))
                Directory.Delete(dir, recursive: true);
    }

    private static bool IsUpdaterFolder(string updater, string folder)
    {
        string updaterFolder = Path.GetDirectoryName(updater);
        return Path.GetFullPath(updaterFolder) == Path.GetFullPath(folder);
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
        bool hasOtherFiles = Directory.GetFiles(folder).Length > MaxFiles;
        if (isPulsarInstall && !hasOtherFiles)
            return true;

        return ContinuePrompt(folder);
    }

    private static bool ContinuePrompt(string folder)
    {
        string caption = "Pulsar Updater";
        string message =
            "The installation folder could not be validated!\n"
            + "Is this your Pulsar install folder?\n"
            + "It WILL BE CLEANED if you update!\n\n"
            + folder;

        DialogResult response = MessageBox.Show(
            message,
            caption,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        return response == DialogResult.Yes;
    }
}
