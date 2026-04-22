using HarmonyLib;
using Keen.Game2.Simulation.RuntimeSystems.Saves;
using Pulsar.Shared;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameSaveInfoSessionComponent), "UsedDebugMenu", MethodType.Getter)]
internal class Patch_DisableDebugTamperFlag
{
    private static bool Prefix(ref bool __result)
    {
        if (Flags.DebugMenu)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
