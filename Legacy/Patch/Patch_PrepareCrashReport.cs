using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using VRage;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(
    "VRage.Platform.Windows.MyCrashReporting, VRage.Platform.Windows",
    "PrepareCrashAnalyticsReporting"
)]
internal static class Patch_PrepareCrashReport
{
    public static string SpaceEngineersPath = null;

    public static bool Prefix(
        string logPath,
        bool GDPRConsent,
        CrashInfo info,
        bool isUnsupportedGpu
    )
    {
        // TODO: Replace this with a Pulsar crash screen in the future.

        string report = isUnsupportedGpu ? "-reporX" : "-report";
        string gameName = $"\"{info.GameName}\"";
        string appVersion = $"\"{info.AppVersion}\"";
        string analyticId = $"\"{info.AnalyticId}\"";
        logPath = $"\"{logPath}\"";

        List<string> args = [report, logPath, gameName, appVersion, analyticId];

        ProcessStartInfo startInfo = new()
        {
            UseShellExecute = false,
            FileName = SpaceEngineersPath,
            Arguments = string.Join(" ", args),
        };

        Process.Start(startInfo);
        return false;
    }
}
