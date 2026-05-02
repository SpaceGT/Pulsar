using System.IO;
using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class LocalPluginExtensions
{
    public static void Show(this LocalPlugin localPlugin)
    {
        string file = Path.GetFullPath(localPlugin.Dll);
        if (File.Exists(file))
            Tools.OpenInDesktop(Path.GetDirectoryName(file));
    }
}
