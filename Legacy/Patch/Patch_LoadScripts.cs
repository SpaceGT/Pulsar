using HarmonyLib;
using Pulsar.Legacy.Loader;
using Sandbox.Game.World;
using VRage.Game;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyScriptManager), "LoadScripts")]
public static class Patch_LoadScripts
{
    public static void Postfix(string path, MyModContext mod)
    {
        if (path == MySession.Static.CurrentPath && mod == MyModContext.BaseGame)
            PluginLoader.Instance?.RegisterEntityComponents();
    }
}
