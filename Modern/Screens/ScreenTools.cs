using Avalonia.Controls;
using Keen.Game2;
using Keen.Game2.Client.UI.Library;
using Keen.Game2.Client.UI.Library.Dialogs.OneOptionDialog;
using Keen.Game2.Client.UI.Library.Dialogs.ThreeOptionsDialog;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.Core;
using Keen.VRage.Library.Localization;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.AvaloniaInterface.Effects;

namespace Pulsar.Modern.Screens;

internal static class ScreenTools
{
    public static LocKey GetKeyFromString(string text)
    {
        return new LocKey() { TextId = StringId.Get(text) };
    }

    public static ThreeOptionsDialogDefinition GetDefaultYesNoCancelDialog()
    {
        return new ThreeOptionsDialogDefinition()
        {
            SelectedOption = ThreeOptionsDialogSelectedOption.Confirm,
            ConfirmOption = GetKeyFromString("Yes"),
            DefaultOption = GetKeyFromString("No"),
            CancelOption = GetKeyFromString("Cancel"),
            Title = GetKeyFromString("Please Confirm"),
        };
    }

    public static TwoOptionsDialogDefinition GetDefaultYesNoDialog()
    {
        return new TwoOptionsDialogDefinition()
        {
            SelectedOption = TwoOptionsDialogSelectedOption.Confirm,
            ConfirmOption = GetKeyFromString("Yes"),
            CancelOption = GetKeyFromString("No"),
            Title = GetKeyFromString("Please Confirm"),
        };
    }

    public static OneOptionDialogDefinition GetDefaultOkDialog()
    {
        return new OneOptionDialogDefinition() { ConfirmOption = GetKeyFromString("Yes") };
    }

    public static SharedUIComponent GetSharedUIComponent()
    {
        return Singleton<VRageCore>.Instance.Engine.Get<GameAppComponent>().GetSharedUI();
    }

    public static void PlayClickSound(Control sender)
    {
        Effects.SetSound(sender, "event:/UI/HudClick");
        Effects.SetSound(sender, "");
    }
}
