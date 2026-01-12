using HarmonyLib;
using Keen.VRage.Platform.Windows;
using Pulsar.Shared;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageWindows), "TryCreateSplashScreen")]
internal class Patch_TryCreateSplashScreen
{
    private static bool Prefix()
    {
        if (Flags.SplashType == SplashType.Native)
            return true;
        return false;
    }
}
