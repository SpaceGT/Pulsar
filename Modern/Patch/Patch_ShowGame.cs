using HarmonyLib;
using Keen.VRage.Core;
using Pulsar.Shared.Splash;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageCore), nameof(VRageCore.NotifyApplicationReady))]
internal static class Patch_ShowGame
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix()
    {
        if (SplashManager.Instance is null)
            return;

        System.Threading.Thread.Sleep(250); // Let progress bar finish
        SplashManager.Instance.Delete();
    }
}
