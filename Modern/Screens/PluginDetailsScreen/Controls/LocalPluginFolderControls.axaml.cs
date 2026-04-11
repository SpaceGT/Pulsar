using Avalonia.Controls;

namespace Pulsar.Modern.Screens.PluginDetailsScreen.Controls;

public partial class LocalPluginFolderControls : UserControl
{
    public LocalPluginFolderControls()
    {
        InitializeComponent();
    }

    private void RemoveFileButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.RemoveDataFile();
    }

    private void LoadFileButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.ShowLoadDataFileScreen();
    }
}
