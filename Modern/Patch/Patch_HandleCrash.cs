using System;
using System.Windows.Forms;
using HarmonyLib;
using Keen.VRage.Core.Platform.CrashReporting;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Platform.Windows;
using Pulsar.Shared;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(CrashHandler), "HandlePrimaryException")]
internal class Patch_HandleCrash
{
    private static bool Prefix(Exception ex)
    {
        Log.Default.Flush();
        Log.Default.WriteLine(ex);
        LogFile.GameLog.Write("Game has crashed.\n" +
        "Space Engineers 2 encountered an unexpected error that was not handled.\n" +
        "The game has now closed as it can no longer proceed safely.\n" +
        "Try running the game without Pulsar to see if this resolves the issue.\n" +
        "Do NOT report this crash to Keen, as the crash may be cased by plugins or Pulsar.\n" +
        "Instead, report this crash in the support fourm on Pulsar's Discord server.");
        Log.Default.Flush();

        DialogResult result = Pulsar.Shared.Tools.ShowMessageBox("Space Engineers 2 encountered an unhandled error.\n" +
                                                                "The game will now closed as it can no longer proceed safely.\n" +
                                                                "Try running the game without Pulsar to see if this resolves the issue.\n" +
                                                                "Do NOT report this crash to Keen, as the crash may be cased by plugins or Pulsar.\n" +
                                                                "Instead, report this crash in Pulsar's Discord server.\n" +
                                                                "Do you want to the open the Pulsar's and the game's log now?", 
                                                                MessageBoxButtons.YesNo, MessageBoxIcon.Error);

        if (result == DialogResult.Yes)
        {
            if (LogFile.GameLog?.Exists() ?? false)
                LogFile.GameLog.Open();

            LogFile.Open();
        }

        Environment.Exit(-1);
        return false;
    }
}
