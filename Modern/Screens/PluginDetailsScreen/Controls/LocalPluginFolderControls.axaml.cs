using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.PluginDetailsScreen.Controls;

public partial class LocalPluginFolderControls : UserControl
{
    public LocalPluginFolderControls()
    {
        InitializeComponent();

        BuildConfigComboBox.Items.Add("Release");
        BuildConfigComboBox.Items.Add("Debug");
    }

    private void BuildConfigComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void RemoveFileButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData as LocalFolderPlugin).DeserializeFile(null);
        (DataContext as PluginDetailsScreenViewModel).Plugin.DataFile = null;
        RemoveFileButton.IsEnabled = false;
    }

    private void LoadFileButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData as LocalFolderPlugin).LoadNewDataFile(
            (file) =>
            {
                (DataContext as PluginDetailsScreenViewModel).Plugin.DataFile = file;
            });
    }
}