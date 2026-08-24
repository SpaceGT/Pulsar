using HarmonyLib;
using Keen.Game2.Simulation.RuntimeSystems.Saves;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameSaveInfoSessionComponent), "UsedDebugMenu", MethodType.Getter)]
internal class Patch_DisableDebugTamperFlag
{
    private static bool Prefix(ref bool __result)
    {
        if (Flags.Current.DebugMenu)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
