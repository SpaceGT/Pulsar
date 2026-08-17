using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using Microsoft.CodeAnalysis.CSharp;
using Pulsar.Legacy.Loader;
using VRage.Game.VisualScripting.ScriptBuilder;
using VRage.Scripting;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
internal static class Patch_Rewriter
{
    internal static readonly ConcurrentDictionary<PluginInstance, MethodInfo> Methods = new();

    [ThreadStatic]
    private static MyApiTarget? target;

    [HarmonyPatch(typeof(MyScriptCompiler), nameof(MyScriptCompiler.Compile))]
    [HarmonyPrefix]
    private static void CaptureTarget(MyApiTarget target) => Patch_Rewriter.target = target;

    [HarmonyPatch(typeof(MyScriptCompiler), "CreateCompilation")]
    [HarmonyPostfix]
    private static void RewriteScript(ref CSharpCompilation __result)
    {
        MyApiTarget current = target ?? MyApiTarget.None;
        target = null;
        __result = Rewrite(__result, current);
    }

    [HarmonyPatch(typeof(MyVSCompiler), nameof(MyVSCompiler.Compile))]
    [HarmonyPostfix]
    private static void RewriteVisualScript(bool __result, ref CSharpCompilation ___m_compilation)
    {
        if (__result)
            ___m_compilation = Rewrite(___m_compilation, MyApiTarget.None);
    }

    private static CSharpCompilation Rewrite(CSharpCompilation compilation, MyApiTarget target)
    {
        foreach (var rewriter in Methods)
        {
            object result;
            try
            {
                result = rewriter.Value.Invoke(null, [compilation, target]);
            }
            catch (Exception e)
            {
                Exception cause = e;
                if (e is TargetInvocationException { InnerException: not null } invocation)
                    cause = invocation.InnerException;

                Disable(rewriter.Key, cause.ToString());
                continue;
            }

            if (result is not CSharpCompilation rewritten)
            {
                Disable(rewriter.Key, "Invalid return value");
                continue;
            }

            compilation = rewritten;
        }

        return compilation;
    }

    private static void Disable(PluginInstance owner, string cause)
    {
        if (!Methods.TryRemove(owner, out _))
            return;

        owner.ThrowError($"Rewriter plugin '{owner}' failed: {cause}");
    }
}
