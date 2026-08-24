using HarmonyLib;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;
using VRage.Platform.Windows.Sys;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(
    typeof(MySingleProgramInstance),
    nameof(MySingleProgramInstance.IsSingleInstance),
    MethodType.Getter
)]
internal static class Patch_MySingleProgramInstance
{
    private static void Postfix(ref bool __result) => __result |= Flags.Current.MultiInstance;
}
