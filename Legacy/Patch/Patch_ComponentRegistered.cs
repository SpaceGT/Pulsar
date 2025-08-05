using System.Reflection;
using HarmonyLib;
using Pulsar.Legacy.Loader;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Plugins;

namespace Pulsar.Legacy.Patch
{
    [HarmonyPatchCategory("Early")]
    [HarmonyPatch(typeof(MySession), "RegisterComponentsFromAssembly")]
    [HarmonyPatch([typeof(Assembly), typeof(bool), typeof(MyModContext)])]
    public static class Patch_ComponentRegistered
    {
        public static void Prefix(Assembly assembly)
        {
            if (assembly == MyPlugins.GameAssembly)
                Plugin.Instance?.RegisterComponents();
        }
    }
}
