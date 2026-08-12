using System.Text;
using HarmonyLib;
using Pulsar.Shared;
using VRage.Utils;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyLog), "GetLogName")]
internal class Patch_StableLogs
{
    private static bool Prefix(string appName, ref StringBuilder __result)
    {
        if (!Flags.StableLogs)
            return true;

        __result = new StringBuilder(appName).Append(".log");
        return false;
    }
}
