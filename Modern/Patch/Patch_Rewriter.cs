using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Scripting;
using Keen.VRage.Scripting.Compilation;
using Microsoft.CodeAnalysis.CSharp;
using Pulsar.Modern.Loader;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(ScriptCompiler), "PostProcessScripts")]
internal static class Patch_Rewriter
{
    internal static readonly ConcurrentDictionary<PluginInstance, MethodInfo> Methods = new();

    private static void Prefix(ICompilationDescriptor descriptor, ref CSharpCompilation compilation)
    {
        if (descriptor is CompilationDescriptorBase compilationDescriptor)
            compilation = Rewrite(compilation, compilationDescriptor.Target);
    }

    private static CSharpCompilation Rewrite(CSharpCompilation compilation, ScriptingTarget target)
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
