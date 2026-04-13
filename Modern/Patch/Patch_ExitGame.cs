using HarmonyLib;
using Keen.VRage.Core;
using System.Diagnostics;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageCore), "Exit")]
internal class Patch_ExitGame
{
    private static bool Prefix()
    {
        Process.GetCurrentProcess().Kill();
        return false;
    }
}
