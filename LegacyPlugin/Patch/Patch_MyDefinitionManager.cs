using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Pulsar.Legacy.Plugin.Extensions;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Definitions;
using VRage.Game;

namespace Pulsar.Legacy.Plugin.Patch
{
    [HarmonyPatch(typeof(MyDefinitionManager), "LoadData")]
    public static class Patch_MyDefinitionManager
    {
        public static void Prefix(ref List<MyObjectBuilder_Checkpoint.ModItem> mods)
        {
            try
            {
                HashSet<ulong> currentMods = [.. mods.Select(x => x.PublishedFileId)];
                List<MyObjectBuilder_Checkpoint.ModItem> newMods = [.. mods];

                foreach (PluginData data in ConfigManager.Instance.Config.EnabledPlugins)
                {
                    if (
                        data is ModPlugin mod
                        && !currentMods.Contains(mod.WorkshopId)
                        && mod.Exists
                    )
                    {
                        LogFile.WriteLine("Loading client mod definitions for " + mod.WorkshopId);
                        newMods.Add(mod.GetModItem());
                    }
                }

                mods = newMods;
            }
            catch (Exception e)
            {
                LogFile.Error("An error occured while loading client mods: " + e);
                throw;
            }
        }
    }
}
