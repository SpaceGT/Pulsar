using HarmonyLib;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Filesystem;
using Keen.VRage.Library.Filesystem.StorageManagers;
using Pulsar.Shared;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(LogManager), nameof(LogManager.GetLogFile))]
internal class Patch_StableLogs
{
    private static bool Prefix(
        string filenameSuffix,
        string ____filenamePrefix,
        ref FileHandleWritable __result
    )
    {
        if (!Flags.StableLogs)
            return true;

        string fileName = ____filenamePrefix.TrimEnd('_') + filenameSuffix + ".log";
        __result = TempStorageManager.Instance.GetFileHandleWritable("Logs\\" + fileName);
        return false;
    }
}
