using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Pulsar.Updater;

internal static class Writer
{
    public static void Update(ZipArchive source, string destination)
    {
        CleanFolder(destination, ["Legacy", "Modern"]);
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
}
