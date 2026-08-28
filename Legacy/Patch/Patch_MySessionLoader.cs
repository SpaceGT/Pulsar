using HarmonyLib;
using Pulsar.Legacy.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Arguments;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using VRage.Game;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Early")]
internal class Patch_MySessionLoader
{
    private static bool Prepare() => Flags.Current.TrustedMods;

    [HarmonyPatch(typeof(MySessionLoader), "LoadMultiplayerScenarioWorld")]
    [HarmonyPrefix]
    public static void Patch_LoadMultiplayerScenarioWorld(
        MyObjectBuilder_World world,
        MyMultiplayerBase multiplayerSession
    )
    {
        world.Checkpoint.Mods.RemoveAll(SteamMods.IsModUntrusted);
    }

    [HarmonyPatch(typeof(MySessionLoader), "LoadMultiplayerSession")]
    [HarmonyPrefix]
    public static void Patch_LoadMultiplayerSession(
        MyObjectBuilder_World world,
        MyMultiplayerBase multiplayerSession
    )
    {
        world.Checkpoint.Mods.RemoveAll(SteamMods.IsModUntrusted);
    }
}
