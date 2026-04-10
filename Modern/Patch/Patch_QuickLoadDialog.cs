using HarmonyLib;
using Keen.Game2.Client.UI.InGame;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(SessionInGameUISessionComponent), "OpenQuickLoadDialog")]
internal class Patch_QuickLoadDialog
{
    // This is meant to prevent the reload world dialog from
    // conflicting with Pulsar's reload game dialog.
    private static bool Prefix()
    {
        if (Patch_PulsarShortcuts.ReloadKeyPressed)
        {
            Patch_PulsarShortcuts.ReloadKeyPressed = false;
            return false;
        }

        return true;
    }
}
