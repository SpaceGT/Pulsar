#if NETCOREAPP
using Pulsar.Compiler;
using Pulsar.Shared;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Pulsar.Legacy.Compiler;

file class CompilerWrapper : ICompiler
{
    private readonly dynamic instance;

    public CompilerWrapper(dynamic compiler, bool debugBuild)
    {
        compiler.DebugBuild = debugBuild;
        instance = compiler;
    }

    public void Load(Stream s, string name) => instance.Load(s, name);

    public void TryAddDependency(string dll) => instance.TryAddDependency(dll);

    public byte[] Compile(string assemblyName, out byte[] symbols) =>
        instance.Compile(assemblyName, out symbols);
}

file sealed class CompilerLoadContext : AssemblyLoadContext
{
    private readonly string binPath;

    public CompilerLoadContext()
        : base("Pulsar", isCollectible: true)
    {
        string applicationBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        binPath = Path.Combine(applicationBase, "Libraries", "Interim", "Compiler");
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        string targetPath = Path.Combine(binPath, assemblyName.Name) + ".dll";
        return File.Exists(targetPath) ? LoadFromAssemblyPath(targetPath) : null;
    }
}

internal class CompilerFactory(string[] probeDirs, string gameDir, string logDir) : ICompilerFactory
{
    private Assembly compilerAsm = null;
    private AssemblyLoadContext loadContext = null;
    private readonly bool isNative = Tools.IsNative();

    public void Init()
    {
        CreateLoadContext();
        SetupLoadContext([.. References.GetReferences(gameDir)]);
    }

    public ICompiler Create(bool debugBuild = false)
    {
        if (loadContext is null)
            Init();

        Type type = compilerAsm.GetType(typeof(RoslynCompiler).FullName, throwOnError: true);
        return new CompilerWrapper(Activator.CreateInstance(type), debugBuild);
    }

    private void SetupLoadContext(string[] assemblies)
    {
        // Pulsar.Compiler.LogFile.Init(logDir);
        compilerAsm
            .GetType(typeof(Pulsar.Compiler.LogFile).FullName, true)
            .GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, [logDir]);

        // RoslynReferences.Instance
        dynamic instance = compilerAsm
            .GetType(typeof(RoslynReferences).FullName, true)
            .GetField("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);

        foreach (string dir in probeDirs)
            instance.Resolver.AddSearchDirectory(dir);

        if (isNative)
        {
            string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            instance.Resolver.AddSearchDirectory(runtimeDir);
        }

        instance.GenerateAssemblyList(assemblies);
    }

    private void CreateLoadContext()
    {
        loadContext = new CompilerLoadContext();
        string compilerPath = typeof(RoslynCompiler).Assembly.Location;
        compilerAsm = loadContext.LoadFromAssemblyPath(compilerPath);
    }

    public void Dispose()
    {
        compilerAsm = null;
        loadContext?.Unload();
    }
}
#endif
