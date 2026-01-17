using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.TextInputDialog;

[NeedsWindowStyles]
public partial class TextInputDialog : PluginScreenBase
{
    public TextInputDialog()
    {
        InitializeComponent();
    }

    private void TextInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        (DataContext as TextInputDialogViewModel).Text = (sender as TextBox).Text;
    }

    private void OkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace((DataContext as TextInputDialogViewModel).Text))
            (DataContext as TextInputDialogViewModel).OnComplete?.Invoke((DataContext as TextInputDialogViewModel).Text);
        Dispose();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}