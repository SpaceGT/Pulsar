using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Pulsar.Compiler;
using Pulsar.Shared;

namespace Pulsar.Legacy;

file static class References
{
    private static readonly string[] baseEnvironment =
    [
        "System.Xaml",
        "System.Windows.Forms",
        "System.Windows.Forms.DataVisualization",
        "System.Windows.Controls.Ribbon",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "Microsoft.CSharp",
        "0Harmony",
        "Newtonsoft.Json",
        "Mono.Cecil",
    ];

    private static readonly string[] includeGlobs =
    [
        "SpaceEngineers*.dll",
        "VRage*.dll",
        "Sandbox*.dll",
        "ProtoBuf*.dll",
    ];

    private static readonly string[] excludeGlobs = ["VRage.Native.dll"];

    public static IEnumerable<string> GetReferences(string exeLocation)
    {
        foreach (string name in Tools.GetFiles(exeLocation, includeGlobs, excludeGlobs))
            yield return name;

        foreach (string name in baseEnvironment)
            yield return name;
    }
}

internal class CompilerFactory(string[] probeDirs, string gameDir, string logDir) : ICompilerFactory
{
    private AppDomain appDomain = null;

    public void Init()
    {
        string[] refererences = [.. References.GetReferences(gameDir)];
        appDomain = CreateAppDomain(refererences, probeDirs, logDir);
    }

    public ICompiler Create(bool debugBuild = false)
    {
        if (appDomain is null)
            Init();

        RoslynCompiler instance = (RoslynCompiler)
            appDomain.CreateInstanceAndUnwrap(
                typeof(RoslynCompiler).Assembly.FullName,
                typeof(RoslynCompiler).FullName
            );

        instance.DebugBuild = debugBuild;
        return instance;
    }

    private static void SetupAppDomain()
    {
        var assemblies = (string[])AppDomain.CurrentDomain.GetData("assemblies");
        var probeDirs = (string[])AppDomain.CurrentDomain.GetData("probeDirs");
        var logDir = (string)AppDomain.CurrentDomain.GetData("logDir");
        Compiler.LogFile.Init(logDir);

        foreach (string dir in probeDirs)
            RoslynReferences.Instance.Resolver.AddSearchDirectory(dir);

        string wpfDir = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "WPF");
        RoslynReferences.Instance.Resolver.AddSearchDirectory(wpfDir);

        RoslynReferences.Instance.GenerateAssemblyList(assemblies);
    }

    private static AppDomain CreateAppDomain(string[] assemblies, string[] probeDirs, string logDir)
    {
        string compilerDir = Path.GetDirectoryName(typeof(RoslynCompiler).Assembly.Location);
        string currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string compilerName = typeof(RoslynCompiler).Assembly.GetName().Name;

        AppDomainSetup current = AppDomain.CurrentDomain.SetupInformation;
        AppDomainSetup config = new()
        {
            ApplicationBase = currentDir,
            PrivateBinPath = null, // Set in the ConfigurationFile
            ConfigurationFile = Path.Combine(compilerDir, compilerName + ".dll.config"),
        };

        AppDomain domain = AppDomain.CreateDomain("Pulsar.Compiler", null, config);

        domain.SetData("probeDirs", probeDirs);
        domain.SetData("logDir", logDir);
        domain.SetData("assemblies", assemblies);
        domain.DoCallBack(SetupAppDomain);

        return domain;
    }

    public void Dispose()
    {
        if (appDomain is not null)
            AppDomain.Unload(appDomain);
    }
}
