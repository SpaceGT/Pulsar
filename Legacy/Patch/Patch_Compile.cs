using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Pulsar.Shared;
using VRage.Scripting;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyScriptCompiler), "AnalyzeDiagnostics")]
public static class Patch_Compile
{
    public static bool PulsarLog = false;
    public static HashSet<string> Diagnostics = [];

    public static void Postfix(
        ImmutableArray<Diagnostic> diagnostics,
        List<Message> messages,
        ref bool success
    )
    {
        if (success || !PulsarLog)
            return;

        // Prevent messages being sent to Patch_MyDefinitionErrors
        messages.Clear();

        IEnumerable<Diagnostic> failures = diagnostics.Where(diagnostic =>
            diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
        );

        foreach (Diagnostic diagnostic in failures)
        {
            Location location = diagnostic.Location;
            string name = CleanFilePath(location.SourceTree?.FilePath);
            LinePosition pos = location.GetLineSpan().StartLinePosition;

            string message = $"{diagnostic.Id}: {diagnostic.GetMessage()}";
            if (name is not null)
                message += $" in file: {name} ({pos.Line + 1},{pos.Character + 1})";

            Diagnostics.Add(message);
        }
    }

    private static string CleanFilePath(string path)
    {
        if (path is null || !path.Contains($"{Steam.AppId}"))
            return path;

        path = path.Substring(path.IndexOf($"{Steam.AppId}"));
        path = Path.Combine([.. path.Split('\\').Skip(5)]);

        return path;
    }
}
