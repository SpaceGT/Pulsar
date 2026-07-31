using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class LocalPluginExtensions
{
    public static void Show(this LocalPlugin localPlugin)
    {
        Tools.ShowInFileManager(localPlugin.Dll);
    }
}
