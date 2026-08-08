using HarmonyLib;
using Pulsar.Shared.Splash;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(
    "Keen.VRage.Platform.Windows.EngineComponents.WinWindowsEngineComponent, VRage.Platform.Windows",
    "OnApplicationReady"
)]
internal static class Patch_ShowGame
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix()
    {
        if (SplashManager.Instance is null)
            return;

        System.Threading.Thread.Sleep(250);
        SplashManager.Instance.Delete();
    }
}
