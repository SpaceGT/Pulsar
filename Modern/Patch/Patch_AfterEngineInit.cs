using Avalonia;
using Avalonia.Input;
using HarmonyLib;
using Keen.Game2;
using Keen.VRage.Client.EngineComponents;
using Keen.VRage.Core;
using Keen.VRage.Library.Utils;
using Keen.VRage.Render.EngineComponents;
using Keen.VRage.UI.AvaloniaInterface;
using Pulsar.Shared;
using Pulsar.Shared.Splash;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameApp), "StartPlayerExperienceAsync")]

// This is the eariest point where we can safely interact with the game's subsystems.
internal static class Patch_AfterEngineInit
{
    public static void Prefix()
    {
        Singleton<VRageCore>.Instance.OnApplicationReady += () => SplashManager.Instance?.Delete();

        if (Flags.DebugMenu)
        {
            AvaloniaApp.Instance.MainWindow?.AttachDevTools(
                new KeyGesture(Key.F12, KeyModifiers.Shift)
            );

            Singleton<VRageCore>.Instance.Engine.Get<DebugMenuEngineComponent>()._debugMenu.IsEnabled = true;
            Singleton<VRageCore>.Instance.Engine.Get<RenderEngineComponent>().RenderContracts.GetRenderSystem().AreDebugCommandsEnabled = true;
        }
            
    }
}
