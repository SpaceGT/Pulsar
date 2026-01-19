using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Keen.Game2;
using Pulsar.Modern.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameApp), "CreateEngine")]
internal class Patch_CreateEngine
{
    private static void Postfix()
    {
        PluginList list = ConfigManager.Instance.List;
        Profile current = ConfigManager.Instance.Profiles.Current;
        IEnumerable<ulong> steamIDs = list.GetModPlugins(current, []).Select(x => x.WorkshopId);
        SteamMods.Update(steamIDs);
    }
}
