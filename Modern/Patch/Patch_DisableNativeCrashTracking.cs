using HarmonyLib;
using Keen.VRage.Core.Platform.CrashReporting;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(CrashMonitorHelper), nameof(CrashMonitorHelper.StartNativeCrashTracking))]
internal class Patch_DisableNativeCrashTracking
{
    private static bool Prefix() => false;
}
