using Avalonia.Controls;
using HarmonyLib;
using Keen.Game2.Client.UI.Menu;
using Keen.Game2.Client.UI.Menu.MainMenu;
using Pulsar.Modern.Screens.PluginsScreen;
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

        Button pluginsButton = __instance.CreateButton(
            "Plugins",
            () => PluginsScreenViewModel.OpenMenu()
        );

        __instance._buttonsPanel.Children.Insert(
            __instance._buttonsPanel.Children.Count - 2,
            pluginsButton
        );

        if (__instance.DataContext is MainMenuScreenViewModel)
            ((Button)__instance._buttonsPanel.Children[^1]).Content =
                $"Exit to {(Tools.IsNative() ? "Windows" : "Linux")}";
    }
}
