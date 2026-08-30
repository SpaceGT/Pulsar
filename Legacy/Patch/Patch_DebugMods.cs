using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;
using VRage.Scripting;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyScriptWhitelist), MethodType.Constructor, [typeof(MyScriptCompiler)])]
internal static class Patch_AllowDebugger
{
    private static bool Prepare() => Flags.Current.DebugMods;

    private static void Postfix(MyScriptWhitelist __instance)
    {
        using IMyWhitelistBatch batch = __instance.OpenBatch();
        batch.AllowTypes(MyWhitelistTarget.ModApi, typeof(Debugger));
    }
}

[HarmonyPatchCategory("Early")]
[HarmonyPatch]
internal static class Patch_LoadPdbs
{
    private static bool Prepare() => Flags.Current.DebugMods;

    private static MethodBase TargetMethod()
    {
        string methodName = nameof(MyScriptCompiler.Compile);
        MethodInfo compileMethod = typeof(MyScriptCompiler).GetMethod(methodName);
        return AccessTools.AsyncMoveNext(compileMethod);
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeInstruction[] code = [.. instructions];

        // Set the local bool loadPDBs to true
        for (int i = 0; i < code.Length - 1; i++)
        {
            CodeInstruction instruction = code[i];
            CodeInstruction nextInstruction = code[i + 1];

            if (
                instruction.opcode == OpCodes.Ldc_I4_0
                && nextInstruction.opcode == OpCodes.Stfld
                && nextInstruction.operand is FieldInfo field
                && field.Name.StartsWith("<loadPDBs>")
            )
                instruction.opcode = OpCodes.Ldc_I4_1;
        }

        return code;
    }
}
