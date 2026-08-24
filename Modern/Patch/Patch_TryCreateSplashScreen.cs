using HarmonyLib;
using Keen.VRage.Platform.Windows;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageWindows), "TryCreateSplashScreen")]
internal class Patch_TryCreateSplashScreen
{
    private static bool Prefix()
    {
        return Flags.Current.SplashType == SplashType.Native;
    }
}
