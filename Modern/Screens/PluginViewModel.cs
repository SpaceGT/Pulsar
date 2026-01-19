using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Modern.Loader;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Modern.Screens;

internal class PluginViewModel : AttachedViewModel
{
    public PluginData PluginData { get; private set; }

    public PluginStat PluginStat { get; set; }

    public string SourceString => PluginData.Source;
    public string VersionString => PluginData.Version?.ToString() ?? "N/A";
    public string StatusString
    {
        get
        {
            if (Design.IsDesignMode)
                return ConfigManager.Instance.SafeMode ? "Disabled" : PluginData.StatusString;
            else
                return PluginData.StatusString;
        }
    }
    public string FriendlyName => PluginData.FriendlyName;
    public string Author => PluginData.Author;
    public string DetailDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(PluginData.Description))
            {
                return PluginData.Description;
            }

            if (!string.IsNullOrEmpty(PluginData.Tooltip))
                return PluginData.Tooltip;

            return "No description";
        }
    }
    public string ShortDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(PluginData.Tooltip))
                return PluginData.Tooltip;

            if (!string.IsNullOrEmpty(PluginData.Description))
                return PluginData.Description;

            return "No description";
        }
    }
    public string ToolTip
    {
        get
        {
            var tip = PluginData.FriendlyName;

            if (!string.IsNullOrWhiteSpace(PluginData.Tooltip))
                tip += "\n" + PluginData.Tooltip;

            return tip;
        }
    }
    public int Players => PluginStat.Players;
    public int Upvotes => PluginStat.Upvotes;
    public int Downvotes => PluginStat.Downvotes;
    public int Vote => PluginStat.Vote;
    public string VoteStatusString
    {
        get
        {
            if (Vote == 0)
                return "You have not voted.";

            if (Vote > 0)
                return "You have upvoted this.";

            return "You have downvoted this.";
        }
    }
    public bool CanVote => PluginData.Enabled || PluginStat.Tried;
    public bool ShowStatElements => !PluginData.IsLocal;

    public bool DraftEnabled
    {
        get { return draft.Contains(PluginData.Id); }
        set
        {
            PluginData.UpdateProfile(draft, value);

            if (!value && PluginData is LocalFolderPlugin devFolder)
                devFolder.DeserializeFile(null);

            OnPropertyChanged(nameof(DraftEnabled));
        }
    }

    public bool HasSettingsMenu
    {
        get
        {
            if (pluginInstance == null)
                return false;

            return pluginInstance.HasConfigDialog;
        }
    }

    public bool IsHidden => PluginData.Hidden;
    public bool IsSupportedRuntime => PluginData.IsSupportedRuntime();

    private readonly Profile draft;
    private readonly PluginInstance pluginInstance;

    public PluginViewModel(PluginData pluginData, Profile draft)
    {
        PluginData = pluginData;
        this.draft = draft;

        if (Design.IsDesignMode)
        {
            PluginStat = new PluginStat();
            return;
        }

        PluginStats stats = ConfigManager.Instance.Stats ?? new PluginStats();
        PluginStat = stats.GetStatsForPlugin(PluginData);

        if (PluginLoader.Instance.TryGetPluginInstance(PluginData.Id, out PluginInstance instance))
            pluginInstance = instance;
    }

    public static PluginViewModel GetDummyPlugin()
    {
        GitHubPlugin dummyPlugin = new()
        {
            Source = "PluginHub",
            Status = PluginStatus.Updated,
            FriendlyName = "A Dummy Plugin",
            Tooltip = "A Dummy Plugin",
            Author = "No One",
            Description =
                "Dummy plugin for Avalonia designer preview\n"
                + "Line2\n"
                + "Line3 https://example.com\n"
                + "LongLine4-------------------------------------------------------\n"
                + "Line5\n"
                + "Line6",
        };

        return new(dummyPlugin, new());
    }

    public void TryOpenSettingsScreen()
    {
        pluginInstance.OpenConfig();
    }

    public void TryVote(int vote)
    {
        if (PlayerConsent.ConsentGiven)
            StoreVote(vote);
        else
            PlayerConsent.ShowDialog(() => StoreVote(vote));
    }

    private void StoreVote(int vote)
    {
        if (!PlayerConsent.ConsentGiven)
            return;

        if (Vote == vote)
            vote = 0;

        PluginStat updatedStat = StatsClient.Vote(PluginData.Id, vote);
        if (updatedStat is null)
            return;

        PluginStats allStats = ConfigManager.Instance.Stats;
        if (allStats is not null)
            allStats.Stats[PluginData.Id] = updatedStat;

        PluginStat = allStats.Stats[PluginData.Id];
        OnPropertyChanged(nameof(Upvotes));
        OnPropertyChanged(nameof(Downvotes));
        OnPropertyChanged(nameof(VoteStatusString));
    }

    public long Rank(string query)
    {
        return PluginData.Rank(query);
    }
}
