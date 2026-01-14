using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Keen.Game2.Game.Plugins;
using Keen.VRage.UI.AvaloniaInterface.Services;
using NuGet.Protocol.Plugins;
using Pulsar.Modern.Extensions;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

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

        TitleText.Text = (DataContext as PluginDetailsScreenViewModel).Plugin.PluginData is ModPlugin ? "Mod Details" : "Plugin Details";

        if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.IsLocal)
        {
            UsersText.IsVisible = false;
            UpvoteButton.IsVisible = false;
            UpvotesText.IsVisible = false;
            DownvoteButton.IsVisible = false;
            DownvotesText.IsVisible = false;
        }

        bool canVote = (DataContext as PluginDetailsScreenViewModel).Plugin.Enabled || (DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Tried;

        if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Vote == 0)
            VoteStatusText.Text = "You have not voted.";
        else if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Vote > 0)
            VoteStatusText.Text = "You have upvoted this.";
        else
            VoteStatusText.Text = "You have downvoted this.";

        UpvoteButton.IsEnabled = canVote;
        DownvoteButton.IsEnabled = canVote;
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
        if (PlayerConsent.ConsentGiven)
            StoreVote(1);
        else
            PlayerConsent.ShowDialog(() => StoreVote(1));
    }

    private void DownvoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (PlayerConsent.ConsentGiven)
            StoreVote(-1);
        else
            PlayerConsent.ShowDialog(() => StoreVote(-1));
    }

    private void StoreVote(int vote)
    {
        if (!PlayerConsent.ConsentGiven)
            return;

        if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Vote == vote)
            vote = 0;

        PluginStat updatedStat = StatsClient.Vote((DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.Id, vote);
        if (updatedStat is null)
            return;

        PluginStats allStats = ConfigManager.Instance.Stats;
        if (allStats is not null)
            allStats.Stats[(DataContext as PluginDetailsScreenViewModel).Plugin.PluginData.Id] = updatedStat;

        (DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat = updatedStat;

        if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Vote == 0)
            VoteStatusText.Text = "You have not voted.";
        else if ((DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Vote > 0)
            VoteStatusText.Text = "You have upvoted this.";
        else
            VoteStatusText.Text = "You have downvoted this.";

        UpvotesText.Text = (DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Upvotes.ToString();
        DownvotesText.Text = (DataContext as PluginDetailsScreenViewModel).Plugin.PluginStat.Downvotes.ToString();
    }
}