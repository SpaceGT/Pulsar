using System;
using System.Windows.Forms;
using HarmonyLib;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
internal static class Patch_FileDialog
{
    private const string TargetFilter = "Folders (*.*)|*.*";
    private const uint FOS_PICKFOLDERS = 0x20;

    [HarmonyPatch(typeof(FileDialog), "GetOptions")]
    [HarmonyPostfix]
    public static void Patch_GetOptions(FileDialog __instance, ref object __result)
    {
        if (__instance.Filter != TargetFilter)
            return;

        uint filePickerFlags = Convert.ToUInt32(__result);
        filePickerFlags |= FOS_PICKFOLDERS;
        __result = Enum.ToObject(__result.GetType(), filePickerFlags);
    }

    [HarmonyPatch(typeof(FileDialog), "SetFileTypes")]
    [HarmonyPrefix]
    public static bool Patch_SetFileTypes(FileDialog __instance)
    {
        return __instance.Filter != TargetFilter;
    }
}
