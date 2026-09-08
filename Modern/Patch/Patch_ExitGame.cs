using System.Diagnostics;
using HarmonyLib;
using Keen.VRage.Core;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(VRageCore), "Exit")]
internal class Patch_ExitGame
{
    private static bool Prefix()
    {
        Launcher.Game.DiscordRpc?.Dispose();
        Process.GetCurrentProcess().Kill();
        return false;
    }
}
