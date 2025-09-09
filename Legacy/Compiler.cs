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
        "Microsoft.CSharp",
        "0Harmony",
        "Newtonsoft.Json",
        "Mono.Cecil",
        "NLog",
    ];

    private static readonly string[] nativeEnvironment =
    [
        "System.Windows.Controls.Ribbon",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
    ];

    private static readonly string[] includeGlobs =
    [
        "SpaceEngineers*.dll",
        "VRage*.dll",
        "Sandbox*.dll",
        "ProtoBuf*.dll",
    ];

    private static readonly string[] excludeGlobs = ["VRage.Native.dll"];

    public static IEnumerable<string> GetReferences(string exeLocation, bool native = true)
    {
        foreach (string name in Tools.GetFiles(exeLocation, includeGlobs, excludeGlobs))
            yield return name;

        foreach (string name in baseEnvironment)
            yield return name;

        if (native)
            foreach (string name in nativeEnvironment)
                yield return name;
        else
            Shared.LogFile.Warn("Ignoring Windows-only references!");
    }
}

internal class CompilerFactory(string[] probeDirs, string gameDir, string logDir) : ICompilerFactory
{
    private AppDomain appDomain = null;
    private readonly bool isNative = Tools.IsNative();

    public void Init()
    {
        string[] refererences = [.. References.GetReferences(gameDir, isNative)];
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
        var isNative = (bool)AppDomain.CurrentDomain.GetData("isNative");
        var logDir = (string)AppDomain.CurrentDomain.GetData("logDir");

        Compiler.LogFile.Init(logDir);

        foreach (string dir in probeDirs)
            RoslynReferences.Instance.Resolver.AddSearchDirectory(dir);

        if (isNative)
        {
            string wpfDir = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "WPF");
            RoslynReferences.Instance.Resolver.AddSearchDirectory(wpfDir);
        }

        RoslynReferences.Instance.GenerateAssemblyList(assemblies);
    }

    private AppDomain CreateAppDomain(string[] assemblies, string[] probeDirs, string logDir)
    {
        // Calling SetupAppDomain requires resolving Pulsar Legacy
        string applicationBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // Mono loads PrivateBinPaths very aggressively so (incompatible) compiler-only
        // references must only be made available to the compiler AppDomain only.
        string privateBinPath = @"Libraries\Legacy\Compiler";

        // Note Mono does not respect AppDomainSetup.ConfigurationFile but NET Framework
        // requires it for binding redirects. Mono (incorrectly) loads compiler-only references
        // if typeof(RoslynCompiler) is called so the path must be hardcoded.
        string configurationFile = @"Libraries\Legacy\Pulsar.Compiler.dll.config";

        AppDomainSetup current = AppDomain.CurrentDomain.SetupInformation;
        AppDomainSetup config = new()
        {
            ApplicationBase = applicationBase,
            PrivateBinPath = privateBinPath,
            ConfigurationFile = configurationFile,
        };

        AppDomain domain = AppDomain.CreateDomain("Pulsar.Compiler", null, config);

        domain.SetData("probeDirs", probeDirs);
        domain.SetData("logDir", logDir);
        domain.SetData("assemblies", assemblies);
        domain.SetData("isNative", isNative);
        domain.DoCallBack(SetupAppDomain);

        return domain;
    }

    public void Dispose()
    {
        if (appDomain is not null)
            AppDomain.Unload(appDomain);
    }
}
