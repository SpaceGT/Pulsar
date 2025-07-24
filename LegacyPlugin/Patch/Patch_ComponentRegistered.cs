using System.Reflection;
using HarmonyLib;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Plugins;

namespace Pulsar.Legacy.Plugin.Patch
{
    [HarmonyPatch(typeof(MySession), "RegisterComponentsFromAssembly")]
    [HarmonyPatch([typeof(Assembly), typeof(bool), typeof(MyModContext)])]
    public static class Patch_ComponentRegistered
    {
        public static void Prefix(Assembly assembly)
        {
            if (assembly == MyPlugins.GameAssembly)
                Main.Instance?.RegisterComponents();
        }
    }
}
