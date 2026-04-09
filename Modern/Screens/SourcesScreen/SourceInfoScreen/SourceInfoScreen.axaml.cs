using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;

[NeedsWindowStyles]
public partial class SourceInfoScreen : PluginScreenBase
{
    public SourceInfoScreen()
    {
        InitializeComponent();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void RemoveButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Title = ScreenTools.GetKeyFromString("Remove Source?");
        definition.Content = ScreenTools.GetKeyFromString(
            $"Are you sure you want to remove {((SourceInfoScreenViewModel)DataContext).SourceName} from the list?"
        );

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TwoOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        ((SourceInfoScreenViewModel)DataContext).RemoveSource();
                        Dispose();
                    },
                }
            );
    }
}