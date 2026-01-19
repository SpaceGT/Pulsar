using Pulsar.Modern.Screens;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class GitHubPluginExtensions
{
    public static void Show(this GitHubPlugin gitHubPlugin)
    {
        ScreenTools.GetSharedUIComponent().OpenUrl($"https://github.com/{gitHubPlugin.RepoId}");
    }
}
