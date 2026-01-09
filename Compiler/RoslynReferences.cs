using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Pulsar.Compiler;

public class RoslynReferences
{
    public static RoslynReferences Instance = new();
    public DefaultAssemblyResolver Resolver = new();

    internal readonly Dictionary<string, MetadataReference> AllReferences = [];

    public void GenerateAssemblyList(IReadOnlyCollection<string> assemblies)
    {
        if (AllReferences.Any())
            return;

        LogFile.WriteLine($"Assembly References:\n{string.Join(", ", assemblies)}");
        LoadAssemblies(assemblies);
    }

    private void LoadAssemblies(IEnumerable<string> names, bool recuse = true)
    {
        HashSet<string> missing = [];
        Stack<string> toProcess = new(names);

        while (toProcess.Any())
        {
            string assembly = toProcess.Pop();

            if (AllReferences.ContainsKey(assembly))
                continue;

            if (!TryLoadAssembly(assembly, out var reference, out var dependencies))
            {
                missing.Add(assembly);
                continue;
            }

            AllReferences[assembly] = reference;

            if (recuse)
                foreach (string name in dependencies)
                    toProcess.Push(name);
        }

        if (missing.Any())
            LogFile.WriteLine($"Skipped References:\n{string.Join(", ", missing)}");
    }

    private bool TryLoadAssembly(
        string name,
        out MetadataReference reference,
        out IEnumerable<string> dependencies
    )
    {
        reference = null;
        dependencies = [];

        AssemblyDefinition definition;
        AssemblyNameReference nameReference = new(name, null);

        try
        {
            definition = Resolver.Resolve(nameReference);
        }
        catch (IOException)
        {
            return false;
        }

        var references = definition.MainModule.AssemblyReferences;
        string fileName = definition.MainModule.FileName;

        reference = MetadataReference.CreateFromFile(fileName);
        dependencies = references.Select(x => x.Name);

        return true;
    }
}
