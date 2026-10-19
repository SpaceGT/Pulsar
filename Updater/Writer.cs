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
    private const int MaxEntries = 20;

    // Kept inside the install folder, so they are on the same volume and writable
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
    private static readonly HashSet<string> Check =
    [
        "Legacy.exe",
        "Interim.exe",
        "Modern.exe",
        "LICENSE",
    ];

    public static void Update(ZipArchive source, string destination)
    {
        if (!Validate(destination))
            Environment.Exit(1);

        string updater = Assembly.GetExecutingAssembly().Location;
        if (IsUpdaterFolder(updater, destination))
            Preserve.Add(Path.GetFileName(updater));

        // Extract next to the old files first: failing here leaves the install untouched
        string newFolder = Path.Combine(destination, NewFolder);
        string oldFolder = Path.Combine(destination, OldFolder);
        DeleteFolder(newFolder);
        DeleteFolder(oldFolder);

        try
        {
            source.ExtractToDirectory(newFolder);
        }
        catch
        {
            TryDeleteFolder(newFolder);
            throw;
        }

        Swap(destination, newFolder, oldFolder);
        TryDeleteFolder(newFolder);
        TryDeleteFolder(oldFolder);
    }

    // Renames only, so there is nothing left to fail on a full disk.
    // On error the old files are put back before the error is passed on.
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
        catch
        {
            foreach (string name in added)
                Move(Path.Combine(destination, name), Path.Combine(newFolder, name));

            foreach (string name in removed)
                Move(Path.Combine(oldFolder, name), Path.Combine(destination, name));

            throw;
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
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
        int entries = GetNames(folder).Count(n => n != NewFolder && n != OldFolder);
        if (isPulsarInstall && entries <= MaxEntries)
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
