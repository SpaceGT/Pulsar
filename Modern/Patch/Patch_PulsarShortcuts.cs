using HarmonyLib;
using Keen.Game2;
using Keen.Game2.Client.UI.HUD.Toolbar;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.Core;
using Keen.VRage.Core.Input;
using Keen.VRage.Input;
using Keen.VRage.Input.EngineComponents;
using Keen.VRage.Library.Utils;
using Pulsar.Modern.Loader;
using Pulsar.Modern.Screens;
using Pulsar.Modern.Screens.PluginConfigurationScreen;
using Pulsar.Modern.Screens.PluginsScreen;
using Pulsar.Shared;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Patch;

[HarmonyPatchCategory("Early")]
[HarmonyPatch(typeof(GameApp), "StartPlayerExperienceAsync")]
internal class Patch_PulsarShortcuts
{
    public static bool ReloadKeyPressed = false;

    private static void Prefix()
    {
        Singleton<VRageCore>
            .Instance.Engine.Get<InputEngineComponent>()
            .DeviceManager.OnBeforeProcessInput += HandlePulsarInput;
    }

    private static void HandlePulsarInput(InputDeviceManager deviceManager)
    {
        IInputDevice keyboard = deviceManager.Keyboard;

        if (keyboard == null)
            return;

        if (!keyboard.HasChanged)
            return;

        if (
            !keyboard.GetDigitalState(KeyboardInputs.Alt)
            || !keyboard.GetDigitalState(KeyboardInputs.Control)
        )
            return;

        ToolbarScreen screen = ScreenTools.FindActiveScreenOfType<ToolbarScreen>();
        PluginsScreen pluginsScreen = ScreenTools.FindActiveScreenOfType<PluginsScreen>();

        if (!(screen?.IsVisible ?? false) && !(pluginsScreen?.IsVisible ?? false))
            return;

        if (keyboard.GetDigitalState(KeyboardInputs.F5))
        {
            AskToRestart();
            ReloadKeyPressed = true;
        }

        if (keyboard.GetDigitalState(KeyboardInputs.OemForwardSlash) && pluginsScreen == null)
            ScreenTools
                .GetSharedUIComponent()
                .CreateScreen<PluginConfigurationScreen>(
                    new PluginConfigurationScreenViewModel(ConfigManager.Instance.List),
                    true
                );

        if (keyboard.GetDigitalState(KeyboardInputs.L))
            AskToShowLogs();
    }

    private static void AskToRestart()
    {
        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Content = ScreenTools.GetKeyFromString("Restart Space Engineers?");

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TwoOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        LoaderTools.AskToRestart();
                    },
                }
            );
    }

    private static void AskToShowLogs()
    {
        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Content = ScreenTools.GetKeyFromString("Show Space Engineers and Pulsar logs?");

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TwoOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        LogFile.GameLog.Open();
                        LogFile.Open();
                    },
                }
            );
    }
}
