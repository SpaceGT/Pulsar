using System.Text;
using HarmonyLib;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;
using VRage.Utils;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyLog), "GetLogName")]
internal class Patch_StableLogs
{
    private static bool Prepare() => Flags.Current.StableLogs;

    private static bool Prefix(string appName, ref StringBuilder __result)
    {
        __result = new StringBuilder(appName).Append(".log");
        return false;
    }
}
