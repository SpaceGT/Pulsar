using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Keen.VRage.UI.AvaloniaInterface.Services;
using NuGet.Protocol.Plugins;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

[NeedsWindowStyles]
public partial class PluginDetailsScreen : PluginScreenBase
{
    public PluginDetailsScreen()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            PluginData dummyPlugin = new GitHubPlugin()
            {
                FriendlyName = "TEST PLUGIN",
                Author = "A user",
                Status = PluginStatus.Updated,
                Tooltip = "TEST MOD DESCRIPTION\nTEST MOD DESCRIPTION\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nLine4\nLine5",
                Source = "Local",
                Description = "TEST MOD DESCRIPTION\nTEST MOD DESCRIPTION\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nLine4\nLine5\nhttps://example.com"
            };

            DataContext = new PluginDetailsScreenViewModel(new PluginViewModel(dummyPlugin, true), null);
            PluginEnabledCheckbox.IsChecked = true;
        }
        else
        {
            PluginEnabledCheckbox.IsChecked = (DataContext as PluginDetailsScreenViewModel).Draft.Contains((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.Id);
        }

        TitleText.Text = (DataContext as PluginDetailsScreenViewModel).Plugin is ModPlugin ? "Mod Details" : "Plugin Details";
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void PluginEnabledCheckbox_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.UpdateProfile((DataContext as PluginDetailsScreenViewModel).Draft, (bool)PluginEnabledCheckbox.IsChecked);

        if (!(bool)PluginEnabledCheckbox.IsChecked && (DataContext as PluginDetailsScreenViewModel).Plugin.PluginData is LocalFolderPlugin devFolder)
            devFolder.DeserializeFile(null);
    }
}