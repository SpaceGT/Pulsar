using HarmonyLib;
using Keen.Game2;
using Pulsar.Modern.Loader;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameApp), "CreateEngine")]
internal class Patch_CreateEngine
{
    private static void Postfix()
    {
        SplashManager.Instance?.SetText($"Updating workshop items...");
        ProfilesConfig profiles = ConfigManager.Instance.Profiles;
        SteamMods.Update(profiles.Current.Mods);
    }
}
