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
[HarmonyPatch(typeof(GameAppComponent), "TransitionToMainMenu")]
internal static class Patch_HideSplash
{
    public static void Postfix()
    {
        SplashManager.Instance?.Delete();

#if !DEBUG
        if (Flags.DebugMenu)
#endif
            (AvaloniaApp.Instance.MainWindow as Window)?.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Shift));
    }
}