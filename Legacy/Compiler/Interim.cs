#if NETCOREAPP
using Pulsar.Compiler;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Pulsar.Legacy.Compiler;

file class CompilerWrapper : ICompiler
{
    private const BindingFlags access = BindingFlags.Public | BindingFlags.Instance;
    private readonly object instance;

    public CompilerWrapper(object compiler, bool debugBuild, string[] flags)
    {
        instance = compiler;
        SetField("DebugBuild", debugBuild);
        SetField("Flags", flags);
    }

    public byte[] Compile(string assemblyName, out byte[] symbols)
    {
        object[] args = [assemblyName, null];
        var output = (byte[])RunMethod("Compile", args);
        symbols = (byte[])args[1];
        return output;
    }

    public void Load(Stream s, string name) => RunMethod("Load", [s, name]);

    public void TryAddDependency(string dll) => RunMethod("TryAddDependency", [dll]);

    private object RunMethod(string name, object[] args) =>
        instance.GetType().GetMethod(name, access).Invoke(instance, args);

    private void SetField(string name, object value) =>
        instance.GetType().GetField(name, access).SetValue(instance, value);
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

    public void Init()
    {
        CreateLoadContext();
        SetupLoadContext([.. References.GetReferences(gameDir)]);
    }

    public ICompiler Create(bool debugBuild = false)
    {
        if (loadContext is null)
            Init();

        string[] flags = debugBuild ? ["NETCOREAPP", "TRACE", "DEBUG"] : ["NETCOREAPP", "TRACE"];

        Type type = compilerAsm.GetType(typeof(RoslynCompiler).FullName, throwOnError: true);
        return new CompilerWrapper(Activator.CreateInstance(type), debugBuild, flags);
    }

    private void SetupLoadContext(string[] assemblies)
    {
        // Pulsar.Compiler.LogFile.Init(logDir);
        compilerAsm
            .GetType(typeof(LogFile).FullName, true)
            .GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, [logDir]);

        // RoslynReferences.Instance
        object instance = compilerAsm
            .GetType(typeof(RoslynReferences).FullName, true)
            .GetField("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);

        MethodInfo generateAssemblyList = instance
            .GetType()
            .GetMethod("GenerateAssemblyList", BindingFlags.Public | BindingFlags.Instance);

        // RoslynReferences.Instance.Resolver
        object resolver = instance
            .GetType()
            .GetField("Resolver", BindingFlags.Public | BindingFlags.Instance)
            .GetValue(instance);

        MethodInfo addSearchDirectory = resolver
            .GetType()
            .GetMethod("AddSearchDirectory", BindingFlags.Public | BindingFlags.Instance);

        // runtimeDir must be probed first to prevent namespace clashes from SE refs.
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        addSearchDirectory.Invoke(resolver, [runtimeDir]);

        foreach (string dir in probeDirs)
            addSearchDirectory.Invoke(resolver, [dir]);

        generateAssemblyList.Invoke(instance, [assemblies]);
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
