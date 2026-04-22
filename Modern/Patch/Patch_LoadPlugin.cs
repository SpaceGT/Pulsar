using System;
using System.Collections.Generic;
using HarmonyLib;
using Keen.Game2.Game.Plugins;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(PluginHost), MethodType.Constructor, [typeof(string[])])]
internal class Patch_LoadPlugin
{
    public static List<Type> PluginsToLoad = [];

    private static void Postfix(PluginHost __instance)
    {
        foreach (var plugin in PluginsToLoad)
        {
            __instance.Add(plugin);
        }
    }
}
