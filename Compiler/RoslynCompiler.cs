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
    public ICompiler Create(bool debugBuild = false);
}

public interface ICompiler
{
    void Load(Stream s, string name);
    public byte[] Compile(string assemblyName, out byte[] symbols);
    public void TryAddDependency(string dll);
}

public class RoslynCompiler : MarshalByRefObject, ICompiler
{
    public bool DebugBuild;

    private readonly List<Source> source = [];
    private readonly PublicizedAssemblies publicizedAssemblies = new();
    private readonly List<MetadataReference> customReferences = [];

    public void Load(Stream s, string name)
    {
        MemoryStream mem = new();
        using (mem)
        {
            s.CopyTo(mem);
            source.Add(new Source(mem, name, DebugBuild));

            SourceText sourceText = SourceText.From(mem);
            publicizedAssemblies.InspectSource(sourceText);
        }
    }

    public byte[] Compile(string assemblyName, out byte[] symbols)
    {
        symbols = null;

        var references = RoslynReferences
            .Instance.AllReferences.Select(kv =>
                publicizedAssemblies.PublicizeReferenceIfRequired(assemblyName, kv.Key, kv.Value)
            )
            .Concat(customReferences);

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: source.Select(x => x.Tree.WithRootAndOptions(x.Tree.GetRoot(), options)),
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
                exceptions.Add(
                    new Exception(
                        $"{diagnostic.Id}: {diagnostic.GetMessage()} in file:\n"
                            + $"{source?.Name ?? "null"} ({pos.Line + 1},{pos.Character + 1})"
                    )
                );
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
                    LogFile.WriteLine("Custom compiler reference: " + (reference.Display ?? dll));
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

        public Source(Stream s, string name, bool includeText)
        {
            Name = name;
            SourceText source = SourceText.From(s, canBeEmbedded: includeText);
            if (includeText)
            {
                Text = EmbeddedText.FromSource(name, source);
                Tree = CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.Latest),
                    name
                );
            }
            else
            {
                Tree = CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.Latest)
                );
            }
        }
    }
}
