using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Extensions;
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

            DataContext = new PluginDetailsScreenViewModel(new PluginViewModel(dummyPlugin, (DataContext as PluginDetailsScreenViewModel).Draft), null);
            PluginEnabledCheckbox.IsChecked = true;
        }
        else
        {
            PluginEnabledCheckbox.IsChecked = (DataContext as PluginDetailsScreenViewModel).Draft.Contains((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.Id);
        }

        TitleText.Text = (DataContext as PluginDetailsScreenViewModel).Plugin.PluginData is ModPlugin ? "Mod Details" : "Plugin Details";
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void MoreInfoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.Show();
    }

    private void SettingsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as PluginDetailsScreenViewModel).PluginInstance?.OpenConfig();
    }

    private void UpvoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as PluginDetailsScreenViewModel).Plugin.TryVote(1);
    }

    private void DownvoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as PluginDetailsScreenViewModel).Plugin.TryVote(-1);
    }
}