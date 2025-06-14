using System.Diagnostics;
using HarmonyLib;
using Sandbox;

namespace avaness.PluginLoader.Patch
{
    [HarmonyPatch(typeof(MySandboxGame), "ExitThreadSafe")]
    public class Patch_ExitThreadSafe
    {
        public static bool Prefix()
        {
            Process.GetCurrentProcess().Kill();
            return false;
        }
    }
}
