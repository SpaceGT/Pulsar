using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Extensions;
using Pulsar.Shared.Data;
using System.Linq;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

[NeedsWindowStyles]
public partial class PluginDetailsScreen : PluginScreenBase
{
    public PluginDetailsScreen()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = new PluginDetailsScreenViewModel(PluginViewModel.GetDummyPlugin());
        }

        TitleText.Text =
            ((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData is ModPlugin
                ? "Mod Details"
                : "Plugin Details";

        if (((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData is GitHubPlugin gitHubPlugin && gitHubPlugin.AlternateVersions is not null && gitHubPlugin.AlternateVersions.Length > 0)
            GithubControls.IsVisible = true;
        else if (((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData is LocalFolderPlugin)
            LocalFolderControls.IsVisible = true;
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void MoreInfoButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData.Show();
    }

    private void SettingsButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.TryOpenSettingsScreen();
    }

    private void UpvoteButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.TryVote(1);
    }

    private void DownvoteButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((PluginDetailsScreenViewModel)DataContext).Plugin.TryVote(-1);
    }
}
