using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class PluginDataExtensions
{
    public static void Show(this PluginData pluginData)
    {
        if (pluginData is LocalFolderPlugin localFolderPlugin)
            localFolderPlugin.Show();
        else if (pluginData is LocalPlugin localPlugin)
            localPlugin.Show();
        else if (pluginData is GitHubPlugin gitHubPlugin)
            gitHubPlugin.Show();
        else if (pluginData is ModPlugin modPlugin)
            modPlugin.Show();
    }
}
