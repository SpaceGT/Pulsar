using Pulsar.Modern.Screens;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Extensions;

internal static class ModPluginExtensions
{
    public static void Show(this ModPlugin modPlugin)
    {
        ScreenTools.GetSharedUIComponent()
            .OpenUrl($"https://steamcommunity.com/workshop/filedetails/?id={modPlugin.WorkshopId}");
    }
}
