using System.Collections.Generic;
using Pulsar.Shared;

namespace Pulsar.Modern;

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
        "SpaceEngineers2.dll",
        "VRage*.dll",
        "Game2*.dll",
    ];

    private static readonly string[] excludeGlobs = ["*.Generator.dll", "*.Native.dll"];

    public static IEnumerable<string> GetReferences(string game2)
    {
        foreach (string name in Tools.GetFiles(game2, game, excludeGlobs))
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
