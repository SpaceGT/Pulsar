using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Sandbox.Game.Screens;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyGuiScreenDownloadMods), "CreateLoadingText")]
static class Patch_CreateLoadingText
{
    const sbyte MaxNameLen = 50;

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Patch magic number in Keen Code
        foreach (CodeInstruction ci in instructions)
        {
            if (ci.opcode == OpCodes.Ldc_I4_S && (sbyte)ci.operand == 25)
                ci.operand = MaxNameLen;

            yield return ci;
        }
    }
}
