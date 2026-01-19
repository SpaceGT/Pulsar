using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.SourcesScreen;

[NeedsWindowStyles]
public partial class SourcesScreen : PluginScreenBase
{
    public SourcesScreen()
    {
        InitializeComponent();
    }

    private void CornerCancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}