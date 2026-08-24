using System.Linq;
using Avalonia.Controls;
using HarmonyLib;
using Keen.Game2.Client.UI.Menu;
using Pulsar.Modern.Screens;
using Pulsar.Modern.Screens.PluginsScreen;
using Pulsar.Shared.Arguments;
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
            ScreenTools.GetKeyFromString("Plugins"),
            PluginsScreenViewModel.OpenMenu
        );
        if (Flags.Current.Profile is not null)
        {
            string message = "Plugins are externally managed by the launch profile";
            pluginsButton.IsEnabled = false;
            ToolTip.SetTip(pluginsButton, message);
        }

        var buttons = __instance._buttonsPanel.Children;
        buttons.Insert(buttons.Count - 2, pluginsButton);

        string host = Tools.IsWindows() && !Tools.IsProton() ? "Windows" : "Linux";
        if (buttons.FirstOrDefault(x => x.Name == "MenuQuit") is Button button)
            button.Content = $"Exit to {host}";

        ScreenTools.GetSharedUIComponent().DialogsConfiguration.ConfirmExitDialog.Title =
            ScreenTools.GetKeyFromString($"Exit To {host}");
    }
}
