namespace Pulsar.Modern.Screens.SourcesScreen.SourceWarningScreen;

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
        (DataContext as SourceWarningScreenViewModel).SaveConfig();
        Dispose();
        (DataContext as SourceWarningScreenViewModel).OnAcknowledge?.Invoke();
    }
}