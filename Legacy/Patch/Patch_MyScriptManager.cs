using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Pulsar.Legacy.Extensions;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Scripting;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyScriptManager), "LoadData")]
public static class Patch_MyScriptManager
{
    private static readonly Action<MyScriptManager, string, MyModContext> loadScripts;
    private static readonly FieldInfo conditionalSymbols;
    private const string ConditionalSymbol = "PULSAR";

    private static HashSet<string> ConditionalSymbols =>
        (HashSet<string>)conditionalSymbols.GetValue(MyScriptCompiler.Static);

    static Patch_MyScriptManager()
    {
        loadScripts =
            (Action<MyScriptManager, string, MyModContext>)
                Delegate.CreateDelegate(
                    typeof(Action<MyScriptManager, string, MyModContext>),
                    typeof(MyScriptManager).GetMethod(
                        "LoadScripts",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                );
        conditionalSymbols = typeof(MyScriptCompiler).GetField(
            "m_conditionalCompilationSymbols",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

#if NETCOREAPP
        ConditionalSymbols.Add("NETCOREAPP");
#endif
    }

    public static void Postfix(MyScriptManager __instance)
    {
        try
        {
            HashSet<ulong> currentMods;
            if (MySession.Static.Mods is not null)
                currentMods = [.. MySession.Static.Mods.Select(x => x.PublishedFileId)];
            else
                currentMods = [];

            HashSet<string> conditionalSymbols = ConditionalSymbols;
            conditionalSymbols.Add(ConditionalSymbol);

            PluginList list = ConfigManager.Instance.List;
            Profile current = ConfigManager.Instance.Profiles.Current;

            Patch_MyDefinitionErrors.RedirectModLogging(true);

            foreach (ModPlugin mod in list.GetModPlugins(current, currentMods))
            {
                LogFile.WriteLine("Loading client mod scripts for " + mod.WorkshopId);
                loadScripts(__instance, mod.ModLocation, mod.GetModContext());
            }

            Patch_MyDefinitionErrors.RedirectModLogging(false);

            conditionalSymbols.Remove(ConditionalSymbol);
        }
        catch (Exception e)
        {
            LogFile.Error("An error occured while loading client mods: " + e);
            throw;
        }
    }
}
