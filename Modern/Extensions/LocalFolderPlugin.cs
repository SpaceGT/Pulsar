using System.IO;
using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class LocalFolderPluginExtensions
{
    public static void Show(this LocalFolderPlugin localFolderPlugin)
    {
        string folder = Path.GetFullPath(localFolderPlugin.Folder);
        if (Directory.Exists(folder))
            Tools.OpenInDesktop(folder);
    }
}
