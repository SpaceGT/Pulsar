using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class LocalFolderPluginExtensions
{
    public static void Show(this LocalFolderPlugin localFolderPlugin)
    {
        Tools.ShowInFileManager(localFolderPlugin.Folder);
    }
}
