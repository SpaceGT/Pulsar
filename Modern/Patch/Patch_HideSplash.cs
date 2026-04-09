using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HarmonyLib;
using Keen.Game2;
using Keen.VRage.UI.AvaloniaInterface;
using Pulsar.Shared;
using Pulsar.Shared.Splash;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameApp), "StartPlayerExperienceAsync")]
internal static class Patch_HideSplash
{
    public static void Prefix()
    {
        SplashManager.Instance?.Delete();

        if (Flags.DebugMenu)
            AvaloniaApp.Instance.MainWindow?.AttachDevTools(
                new KeyGesture(Key.F12, KeyModifiers.Shift)
            );
    }
}
