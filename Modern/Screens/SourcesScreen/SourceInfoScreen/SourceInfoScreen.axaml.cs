using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
}