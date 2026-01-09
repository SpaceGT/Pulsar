using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Pulsar.Compiler;

public interface ICompilerFactory : IDisposable
{
    void Init();
    ICompiler Create(bool debugBuild = false);
}

public interface ICompiler
{
    void Load(Stream s, string name, string embedFile = null);
    byte[] Compile(string assemblyName, out byte[] symbols);
    void TryAddDependency(string dll);
}

public class RoslynCompiler : MarshalByRefObject, ICompiler
{
    public bool DebugBuild;
    public string[] Flags;

    private readonly List<Source> source = [];
    private readonly PublicizedAssemblies publicizedAssemblies = new();
    private readonly List<MetadataReference> customReferences = [];

    public void Load(Stream s, string name, string embedFile = null)
    {
        var options = CSharpParseOptions
            .Default.WithLanguageVersion(LanguageVersion.CSharp13)
            .WithPreprocessorSymbols(Flags);

        using MemoryStream mem = new();

        s.CopyTo(mem);
        source.Add(new Source(mem, name, options, embedFile));

        SourceText sourceText = SourceText.From(mem);
        publicizedAssemblies.InspectSource(sourceText);
    }

    public byte[] Compile(string assemblyName, out byte[] symbols)
    {
        symbols = null;

        var references = RoslynReferences
            .Instance.AllReferences.Select(kv =>
                publicizedAssemblies.PublicizeReferenceIfRequired(assemblyName, kv.Key, kv.Value)
            )
            .Concat(customReferences);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: source.Select(x => x.Tree),
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: DebugBuild ? OptimizationLevel.Debug : OptimizationLevel.Release,
                allowUnsafe: true
            )
        );

        using MemoryStream pdb = new();
        using MemoryStream ms = new();

        // write IL code into memory
        EmitResult result;
        if (DebugBuild)
        {
            result = compilation.Emit(
                ms,
                pdb,
                embeddedTexts: source.Select(x => x.Text),
                options: new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: Path.ChangeExtension(assemblyName, "pdb")
                )
            );
        }
        else
        {
            result = compilation.Emit(ms);
        }

        if (!result.Success)
        {
            // handle exceptions
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
            );

            List<Exception> exceptions = [];
            foreach (Diagnostic diagnostic in failures)
            {
                Location location = diagnostic.Location;
                Source source = this.source.FirstOrDefault(x => x.Tree == location.SourceTree);
                LinePosition pos = location.GetLineSpan().StartLinePosition;

                string message = $"{diagnostic.Id}: {diagnostic.GetMessage()}";
                if (source?.Name is not null)
                    message += $" in file: {source?.Name} ({pos.Line + 1},{pos.Character + 1})";

                exceptions.Add(new Exception(message));
            }
            throw new AggregateException("Compilation failed!", exceptions);
        }
        else
        {
            if (DebugBuild)
            {
                pdb.Seek(0, SeekOrigin.Begin);
                symbols = pdb.ToArray();
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }
    }

    public void TryAddDependency(string dll)
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

    private class Source
    {
        public string Name { get; }
        public SyntaxTree Tree { get; }
        public EmbeddedText Text { get; }

        public Source(Stream s, string name, CSharpParseOptions options, string embedFile = null)
        {
            Name = name;
            bool includeText = embedFile is not null;
            SourceText source = SourceText.From(s, canBeEmbedded: includeText);

            if (includeText)
            {
                Text = EmbeddedText.FromSource(embedFile, source);
                Tree = CSharpSyntaxTree.ParseText(source, options, embedFile);
            }
            else
            {
                Tree = CSharpSyntaxTree.ParseText(source, options);
            }
        }
    }
}
