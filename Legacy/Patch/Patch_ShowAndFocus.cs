using HarmonyLib;

namespace Pulsar.Legacy.Patch
{
    [HarmonyPatchCategory("Early")]
    [HarmonyPatch(
        "VRage.Platform.Windows.Forms.MyGameWindow, VRage.Platform.Windows",
        "ShowAndFocus"
    )]
    public static class Patch_ShowAndFocus
    {
        public static bool Enabled = false;

        public static bool Prefix() => Enabled;
    }
}
