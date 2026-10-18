using System;
using Pulsar.Shared.Config;

namespace Pulsar.Shared;

/// <summary>
/// Status lines for the Plugins dialog.
/// </summary>
public static class PulsarInfo
{
    private static Version Current => Tools.PulsarAssembly.Version;

    public static string VersionText => $"Pulsar {Tools.Flavour} {Current.ToString(3)}";

    /// <summary>
    /// Null unless the update check found a newer release.
    /// </summary>
    public static string UpdateText =>
        Updater.LatestVersion > Current
            ? $"Update available: {Updater.LatestVersion.ToString(3)}"
            : null;

    public const string ListCheckedLabel = "Plugin list checked:";

    public static string ListCheckedTime(SourcesConfig sources) =>
        sources.LastRemoteCheck is DateTime time
            ? $"{time.ToLocalTime():yyyy-MM-dd HH:mm}"
            : "never";

    public static string ListCheckedToolTip(SourcesConfig sources) =>
        "When Pulsar last asked GitHub whether the plugin list changed.\n"
        + $"It asks again once {sources.MaxSourceAge} hours have passed.";
}
