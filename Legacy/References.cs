using System.Collections.Generic;
using Pulsar.Shared;

namespace Pulsar.Legacy;

internal static class References
{
    private static readonly string[] common =
    [
        "Microsoft.CSharp",
        "0Harmony",
        "Newtonsoft.Json",
        "Mono.Cecil",
        "NLog",
    ];

    private static readonly string[] winforms =
    [
        "System.Windows.Forms",
#if NETFRAMEWORK
        "System.Windows.Forms.DataVisualization",
#endif
    ];

    private static readonly string[] wpf =
    [
        "System.Xaml",
        "System.Windows.Controls.Ribbon",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
    ];

    private static readonly string[] game =
    [
        "SpaceEngineers*.dll",
        "VRage*.dll",
        "Sandbox*.dll",
        "ProtoBuf*.dll",
    ];

    private static readonly string[] excludeGlobs = ["VRage.Native.dll"];

    public static IEnumerable<string> GetReferences(string bin64)
    {
        foreach (string name in Tools.GetFiles(bin64, game, excludeGlobs))
            yield return name;

        foreach (string name in common)
            yield return name;

        if (!Tools.IsWindows())
            yield break;

        foreach (string name in winforms)
            yield return name;

        foreach (string name in wpf)
            yield return name;
    }
}
