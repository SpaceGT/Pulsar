using System;
using HarmonyLib;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Platform.Windows;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageWindows), "RestartToReport")]
internal class Patch_RestartToReport
{
    private static bool Prefix()
    {
        Log.Default.Flush();
        Log.Default.WriteLine("[Pulsar]: Game has crashed.");
        Log.Default.WriteLine(
            "[Pulsar]: If you are not expacting a crash, please try running the game without Pulsar."
        );
        Log.Default.WriteLine(
            "[Pulsar]: If that resolves the issue, please submit a report in the Pulsar Discord server so the issue can be fixed."
        );
        Log.Default.WriteLine(
            "[Pulsar]: Plugins can cause the game to crash for many reasons outside Keen's control, so crash reporting has been disabled."
        );
        Log.Default.Flush();

        Environment.Exit(-1);
        return false;
    }
}
