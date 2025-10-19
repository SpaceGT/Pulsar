using HarmonyLib;
using Pulsar.Legacy.Loader;
using Sandbox.Game.World;
using VRage.Game;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyScriptManager), "LoadScripts")]
public static class Patch_LoadScripts
{
    public static void Postfix(MyScriptManager __instance, string path, MyModContext mod)
    {
        // piggyback off of the base game script loading
        if (path == MySession.Static.CurrentPath && mod == MyModContext.BaseGame)
        {
            // load entity components from plugins
            PluginLoader.Instance?.RegisterEntityScripts(__instance);
        }
    }
}
