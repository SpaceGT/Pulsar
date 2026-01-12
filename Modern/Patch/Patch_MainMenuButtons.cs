using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HarmonyLib;
using Keen.Game2.Client.UI.Menu;
using Keen.Game2.Client.UI.Menu.MainMenu;
using Keen.VRage.UI.AvaloniaInterface;
using Pulsar.Modern.Screens.PluginsScreen;
using Pulsar.Shared;
using Tools = Pulsar.Shared.Tools;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(GameMenu), "UpdateButtons")]
internal class Patch_MainMenuButtons
{
    private static void Postfix(GameMenu __instance)
    {
        if (__instance._buttonsPanel == null)
        {
            return;
        }

        Button pluginsButton = __instance.CreateButton("Plugins", () => PluginsScreenViewModel.Open());

        __instance._buttonsPanel.Children.Insert(__instance._buttonsPanel.Children.Count - 2, pluginsButton);

        if (__instance.DataContext is MainMenuScreenViewModel)
            (__instance._buttonsPanel.Children[__instance._buttonsPanel.Children.Count - 1] as Button).Content = $"Exit to {(Tools.IsNative() ? "Windows" : "Linux")}";

        if (Flags.DebugMenu)
            (AvaloniaApp.Instance.MainWindow as Window)?.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Shift));
    }
}
