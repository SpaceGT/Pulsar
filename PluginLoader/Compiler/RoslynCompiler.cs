using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace avaness.PluginLoader.Compiler
{
    public class RoslynCompiler
    {
        private readonly List<Source> source = new List<Source>();
        private readonly PublicizedAssemblies publicizedAssemblies = new PublicizedAssemblies();
        private readonly List<MetadataReference> customReferences = new List<MetadataReference>();
        private bool debugBuild;

        public RoslynCompiler(bool debugBuild = false)
        {
            this.debugBuild = debugBuild;
        }

        public void Load(Stream s, string name)
        {
            MemoryStream mem = new MemoryStream();
            using (mem)
            {
                s.CopyTo(mem);
                source.Add(new Source(mem, name, debugBuild));

                SourceText sourceText = SourceText.From(mem);
                publicizedAssemblies.InspectSource(sourceText);
            }
        }

        public byte[] Compile(string assemblyName, out byte[] symbols)
        {
            symbols = null;
            
            var references = RoslynReferences.AllReferences
                .Select(kv => publicizedAssemblies.PublicizeReferenceIfRequired(assemblyName, kv.Key, kv.Value))
                .Concat(customReferences)
                .ToHashSet();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: source.Select(x => x.Tree),
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: debugBuild ? OptimizationLevel.Debug : OptimizationLevel.Release,
                    allowUnsafe: true));

            using (MemoryStream pdb = new MemoryStream())
            using (MemoryStream ms = new MemoryStream())
            {
                // write IL code into memory
                EmitResult result;
                if (debugBuild)
                {
                    result = compilation.Emit(ms, pdb,
                        embeddedTexts: source.Select(x => x.Text),
                        options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: Path.ChangeExtension(assemblyName, "pdb")));
                }
                else
                {
                    result = compilation.Emit(ms);
                }

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Location location = diagnostic.Location;
                        Source source = this.source.FirstOrDefault(x => x.Tree == location.SourceTree);
                        LinePosition pos = location.GetLineSpan().StartLinePosition;
                        LogFile.Error($"{diagnostic.Id}: {diagnostic.GetMessage()} in file:\n{source?.Name ?? "null"} ({pos.Line + 1},{pos.Character + 1})");
                    }
                    throw new Exception("Compilation failed!");
                }
                else
                {
                    if (debugBuild)
                    {
                        pdb.Seek(0, SeekOrigin.Begin);
                        symbols = pdb.ToArray();
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    return ms.ToArray();
                }
            }

        }

        public void TryAddDependency(string dll)
        {
            if (Path.HasExtension(dll)
                && Path.GetExtension(dll).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                && File.Exists(dll))
            {
                try
                {
                    MetadataReference reference = MetadataReference.CreateFromFile(dll);
                    if (reference != null)
                    {
                        LogFile.WriteLine("Custom compiler reference: " + (reference.Display ?? dll));
                        customReferences.Add(reference);
                    }
                }
                catch
                { }
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
                    Tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), name);
                }
                else
                {
                    Tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
                }
            }
        }
    }

}
