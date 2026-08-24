using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Scripting;
using Keen.VRage.Scripting.Compilation;
using Microsoft.CodeAnalysis.CSharp;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(ScriptCompiler), nameof(ScriptCompiler.CompileAsync))]
internal static class Patch_DebugMods
{
    private static void Prefix(ICompilationDescriptor descriptor)
    {
        if (
            !Flags.Current.DebugMods
            || descriptor is not CompilationDescriptorBase compilation
            || compilation.Target != ScriptingTarget.GameMod
        )
            return;

        compilation.EnableDebugging();

        CSharpParseOptions options = descriptor.ParseOptions;
        IEnumerable<string> symbols = options.PreprocessorSymbolNames.Append("DEBUG").Distinct();
        options = options.WithPreprocessorSymbols(symbols);

        string propertyName = nameof(CompilationDescriptorBase.ParseOptions);
        PropertyInfo property = typeof(CompilationDescriptorBase).GetProperty(propertyName);
        property.SetValue(compilation, options);
    }
}
