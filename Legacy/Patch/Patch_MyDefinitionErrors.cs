using HarmonyLib;
using Pulsar.Shared;
using VRage.FileSystem;
using VRage.Game;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(MyDefinitionErrors), "Add")]
public static class Patch_MyDefinitionErrors
{
    public static bool PulsarLog = false;

    public static bool Prefix(MyModContext context, string message)
    {
        if (!PulsarLog || !message.Contains("Compilation"))
            return true;

        string[] trim = [$"Compilation of {MyFileSystem.ModsPath}\\{context.ModId}_", " failed:"];
        string name = Tools.RemoveAll(message, trim);

        LogFile.Error($"Failed to build {name}:");
        foreach (string diagnostic in Patch_Compile.Diagnostics)
            LogFile.Error(diagnostic);

        return false;
    }

    public static void RedirectModLogging(bool enabled)
    {
        PulsarLog = enabled;
        Patch_Compile.PulsarLog = enabled;
    }
}
