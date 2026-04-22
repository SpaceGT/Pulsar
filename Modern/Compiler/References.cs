using System.Collections.Generic;
using Pulsar.Shared;

namespace Pulsar.Modern.Compiler;

internal static class References
{
    private static readonly string[] baseEnvironment =
    [
        "System.Xaml",
        "System.Windows.Forms",
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
        "SpaceEngineers2.dll",
        "VRage*.dll",
        "Game2*.dll",
    ];

    private static readonly string[] excludeGlobs = ["*.Generator.dll", "*.Native.dll"];

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
            LogFile.Warn("Ignoring Windows-only references!");
    }
}
