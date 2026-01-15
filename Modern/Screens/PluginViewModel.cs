using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Modern.Screens
{
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
            get
            {
                return draft.Contains(PluginData.Id);
            }
            set
            {
                PluginData.UpdateProfile(draft, value);

                if (!value && PluginData is LocalFolderPlugin devFolder)
                    devFolder.DeserializeFile(null);

                OnPropertyChanged(nameof(DraftEnabled));
            }
        }

        private Profile draft;

        public PluginViewModel(PluginData pluginData, Profile draft)
        {
            PluginStats stats = null;

            if (!Design.IsDesignMode)
                stats = ConfigManager.Instance.Stats ?? new PluginStats();

            PluginData = pluginData;
            this.draft = draft;

            if (!Design.IsDesignMode)
                PluginStat = stats.GetStatsForPlugin(PluginData);
            else
                PluginStat = new PluginStat();
        }

        public void TryVote(int vote)
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
    }
}
