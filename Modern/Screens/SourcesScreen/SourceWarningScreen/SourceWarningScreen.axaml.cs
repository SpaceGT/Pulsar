using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.SourcesScreen.SourceWarningScreen;

[NeedsWindowStyles]
public partial class SourceWarningScreen : PluginScreenBase
{
    public SourceWarningScreen()
    {
        InitializeComponent();
    }

    private void CancelButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void AcknowledgeButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((SourceWarningScreenViewModel)DataContext).SaveConfig();
        Dispose();
        ((SourceWarningScreenViewModel)DataContext).OnAcknowledge?.Invoke();
    }
}
