using HarmonyLib;
using Keen.Game2;
using System.Diagnostics;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameAppComponent), "ExitGame")]
internal class Patch_ExitGame
{
    private static bool Prefix()
    {
        Process.GetCurrentProcess().Kill();
        return false;
    }
}
