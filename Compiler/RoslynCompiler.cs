using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Pulsar.Protocol.Compiler;

namespace Pulsar.Compiler;

internal class RoslynCompiler(CompileRequest request)
{
    private readonly List<Source> source = [];
    private readonly PublicizedAssemblies publicizedAssemblies = new();
    private readonly List<MetadataReference> customReferences = [];

    public CompileResponse Compile()
    {
        LoadSources();
        LoadReferences();

        var references = RoslynReferences
            .Instance.AllReferences.Select(kv =>
                publicizedAssemblies.PublicizeReferenceIfRequired(
                    request.AssemblyName,
                    kv.Key,
                    kv.Value
                )
            )
            .Concat(customReferences);

        CSharpCompilation compilation = CSharpCompilation.Create(
            request.AssemblyName,
            syntaxTrees: source.Select(x => x.Tree),
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: request.DebugBuild
                    ? OptimizationLevel.Debug
                    : OptimizationLevel.Release,
                allowUnsafe: true
            )
        );

        using MemoryStream pdb = new();
        using MemoryStream ms = new();

        // write IL code into memory
        EmitResult result;
        if (request.DebugBuild)
        {
            result = compilation.Emit(
                ms,
                pdb,
                embeddedTexts: source.Where(x => x.Text is not null).Select(x => x.Text),
                options: new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: Path.ChangeExtension(request.AssemblyName, "pdb")
                )
            );
        }
        else
        {
            result = compilation.Emit(ms);
        }

        if (!result.Success)
            return GetErrors(result.Diagnostics);

        return new CompileResponse
        {
            Success = true,
            Assembly = ms.ToArray(),
            Symbols = request.DebugBuild ? pdb.ToArray() : null,
        };
    }

    private void LoadSources()
    {
        CSharpParseOptions options = CSharpParseOptions
            .Default.WithLanguageVersion(LanguageVersion.CSharp14)
            .WithPreprocessorSymbols(request.Flags ?? []);

        foreach (SourceFile file in request.Sources ?? [])
        {
            Source item = new(file, options);
            source.Add(item);
            publicizedAssemblies.InspectSource(item.SourceText);
        }
    }

    private void LoadReferences()
    {
        foreach (string dll in request.References ?? [])
            TryAddDependency(dll);
    }

    private void TryAddDependency(string dll)
    {
        if (
            Path.HasExtension(dll)
            && Path.GetExtension(dll).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && File.Exists(dll)
        )
        {
            try
            {
                MetadataReference reference = MetadataReference.CreateFromFile(dll);
                if (reference is not null)
                {
                    LogFile.WriteLine($"Custom compiler reference: {Path.GetFileName(dll)}");
                    customReferences.Add(reference);
                }
            }
            catch { }
        }
    }

    private CompileResponse GetErrors(IEnumerable<Diagnostic> diagnostics)
    {
        IEnumerable<CompilerDiagnostic> failures = diagnostics
            .Where(diagnostic =>
                diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
            )
            .Select(GetError);

        return new CompileResponse { Success = false, Diagnostics = [.. failures] };
    }

    private CompilerDiagnostic GetError(Diagnostic diagnostic)
    {
        Location location = diagnostic.Location;
        Source item = source.FirstOrDefault(x => x.Tree == location.SourceTree);
        LinePosition pos = location.GetLineSpan().StartLinePosition;

        return new CompilerDiagnostic
        {
            Id = diagnostic.Id,
            Message = diagnostic.GetMessage(),
            Source = item?.Name,
            Line = pos.Line + 1,
            Column = pos.Character + 1,
        };
    }

    private class Source
    {
        public string Name { get; }
        public SyntaxTree Tree { get; }
        public EmbeddedText Text { get; }
        public SourceText SourceText { get; }

        public Source(SourceFile source, CSharpParseOptions options)
        {
            Name = source.Name;
            bool includeText = source.EmbedFile is not null;
            using MemoryStream stream = new(source.Data ?? []);
            SourceText = SourceText.From(stream, canBeEmbedded: includeText);

            if (includeText)
            {
                Text = EmbeddedText.FromSource(source.EmbedFile, SourceText);
                Tree = CSharpSyntaxTree.ParseText(SourceText, options, source.EmbedFile);
            }
            else
            {
                Tree = CSharpSyntaxTree.ParseText(SourceText, options);
            }
        }
    }
}
