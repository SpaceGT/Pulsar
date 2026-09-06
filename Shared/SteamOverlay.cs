using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Pulsar.Shared;

internal static class SteamOverlay
{
    private const string EnableVulkan = "ENABLE_VK_LAYER_VALVE_steam_overlay_1";
    private const string DisableVulkan = "DISABLE_VK_LAYER_VALVE_steam_overlay_1";

    public static void DisableForHelper(ProcessStartInfo startInfo)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        // The Avalonia splash/dialog process must not create a second game overlay.
        // Only change the child's environment; retain other preloaded libraries.
        if (startInfo.Environment.TryGetValue("LD_PRELOAD", out string preload))
        {
            string[] libraries = (preload ?? "").Split(
                [':', ' '],
                StringSplitOptions.RemoveEmptyEntries
            );
            string filtered = string.Join(
                ":",
                libraries.Where(path => Path.GetFileName(path) != "gameoverlayrenderer.so")
            );
            if (filtered.Length == 0)
                startInfo.Environment.Remove("LD_PRELOAD");
            else
                startInfo.Environment["LD_PRELOAD"] = filtered;
        }

        startInfo.Environment.Remove(EnableVulkan);
        startInfo.Environment[DisableVulkan] = "1";
    }
}
