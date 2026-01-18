using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.AddModSourceScreen;

[NeedsWindowStyles]
public partial class AddModSourceScreen : PluginScreenBase
{
    public AddModSourceScreen()
    {
        InitializeComponent();
    }

    private void TextInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        (DataContext as AddModSourceScreenViewModel).Text = (sender as TextBox).Text;
    }

    private void OkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace((DataContext as AddModSourceScreenViewModel).Text))
            (DataContext as AddModSourceScreenViewModel).OnComplete?.Invoke((DataContext as AddModSourceScreenViewModel).Text);
        Dispose();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}