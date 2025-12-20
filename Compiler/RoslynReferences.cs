using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using NLog;

namespace Pulsar.Compiler;

public class RoslynReferences
{
    public static RoslynReferences Instance = new();
    public DefaultAssemblyResolver Resolver = new();

    internal readonly Dictionary<string, MetadataReference> AllReferences = [];
    private readonly HashSet<string> referenceBlacklist =
    [
        // Net Framework Blacklist
        "System.ValueTuple",
        "System.Private.ServiceModel",
        "System.ServiceModel.Syndication",
        // Net Core Blacklist
        "System.ServiceProcess.ServiceController",
        "System.Runtime.Serialization.Schema",
        "System.IO.Ports",
        "System.Data.OleDb",
        "System.Data.Odbc",
        "System.Data.SqlClient",
    ];

    public void GenerateAssemblyList(IReadOnlyCollection<string> assemblies)
    {
        if (AllReferences.Count > 0)
            return;

        StringBuilder sb = new("Assembly References:");
        sb.AppendLine();
        sb.Append(string.Join(", ", assemblies));

        LogLevel level = LogLevel.Info;
        try
        {
            LoadAssemblies(assemblies);
        }
        catch (Exception e)
        {
            sb.AppendLine().Append("Error: ").Append(e).AppendLine();
            level = LogLevel.Error;
        }

        LogFile.WriteLine(sb.ToString(), level);
    }

    public void LoadReference(string name, bool recurse = true)
    {
        try
        {
            LoadAssemblies([name], recurse);
            LogFile.WriteLine("Reference added at runtime: " + name);
        }
        catch (IOException)
        {
            LogFile.Error("Unable to find the assembly '" + name + "'!");
        }
    }

    private void LoadAssemblies(IEnumerable<string> names, bool recuse = true)
    {
        Stack<string> toProcess = new(names);

        while (toProcess.Count > 0)
        {
            string assembly = toProcess.Pop();

            if (referenceBlacklist.Contains(assembly))
                continue;

            if (AllReferences.ContainsKey(assembly))
                continue;

            var (reference, dependencies) = LoadAssembly(assembly);
            AllReferences[assembly] = reference;

            if (recuse)
                foreach (string name in dependencies)
                    toProcess.Push(name);
        }
    }

    private (MetadataReference, IEnumerable<string>) LoadAssembly(string name)
    {
        AssemblyNameReference nameReference = new(name, null);
        AssemblyDefinition definition = Resolver.Resolve(nameReference);

        var references = definition.MainModule.AssemblyReferences;
        string fileName = definition.MainModule.FileName;

        var reference = MetadataReference.CreateFromFile(fileName);
        IEnumerable<string> dependencies = references.Select(x => x.Name);

        return (reference, dependencies);
    }
}
