using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pulsar.Modern.Screens.AddPluginScreen
{
    internal class AddPluginScreenViewModel : ScreenViewModel
    {
        public event Action OnScreenClose;

        public readonly List<PluginData> Plugins = [];
        public readonly List<PluginData> Hidden = [];
        public readonly Profile Draft;
        private PluginStats stats;
        public readonly bool Mods;
        public string Filter;
        public SortingMethod SortMethod = SortingMethod.Name;

        public enum SortingMethod : int
        {
            Name,
            Search,
            Usage,
            Rating,
        }

        public AddPluginScreenViewModel(IEnumerable<PluginData> plugins, bool mods, Profile draft)
        {
            KeepsOtherScreensVisible = false;
            AllowsInputBelowUI = false;
            AllowsInputFromLowerScreens = false;
            InitializeInputContext();

            stats = ConfigManager.Instance.Stats ?? new PluginStats();
            Mods = mods;
            this.Draft = draft;

            var supported = plugins.Where(x => (x is ModPlugin) == mods && x.IsSupportedRuntime());
            this.Plugins = [.. supported.Where(x => !x.Hidden)];
            Hidden = [.. supported.Where(x => x.Hidden)];

            SortPlugins(SortingMethod.Name);
        }

        public override void OnDispose()
        {
            base.OnDispose();

            OnScreenClose?.Invoke();
        }

        public void SortPlugins(SortingMethod sort)
        {
            switch (sort)
            {
                case SortingMethod.Name:
                    Plugins.Sort(ComparePluginsByName);
                    break;
                case SortingMethod.Usage:
                    Plugins.Sort(ComparePluginsByUsage);
                    break;
                case SortingMethod.Rating:
                    Plugins.Sort(ComparePluginsByRating);
                    break;
                case SortingMethod.Search:
                    SortPluginsBySearch();
                    break;
                default:
                    Plugins.Sort(ComparePluginsByName);
                    break;
            }
        }

        public void SortPluginsBySearch()
        {
            if (string.IsNullOrWhiteSpace(Filter))
                return;

            var scoreCache = Plugins.ToDictionary(p => p, p => p.Rank(Filter));
            Plugins.Sort(Comparator);

            int Comparator(PluginData x, PluginData y)
            {
                int comp = scoreCache[y].CompareTo(scoreCache[x]);
                return comp == 0 ? ComparePluginsByName(x, y) : comp;
            }
        }

        private int ComparePluginsByName(PluginData x, PluginData y)
        {
            return x.FriendlyName.CompareTo(y.FriendlyName, StringComparison.OrdinalIgnoreCase);
        }

        private int ComparePluginsByUsage(PluginData x, PluginData y)
        {
            PluginStat statX = stats.GetStatsForPlugin(x);
            PluginStat statY = stats.GetStatsForPlugin(y);
            int usage = -statX.Players.CompareTo(statY.Players);
            if (usage != 0)
                return usage;
            return ComparePluginsByName(x, y);
        }

        private int ComparePluginsByRating(PluginData x, PluginData y)
        {
            PluginStat statX = stats.GetStatsForPlugin(x);
            int ratingX = statX.Upvotes - statX.Downvotes;
            PluginStat statY = stats.GetStatsForPlugin(y);
            int ratingY = statY.Upvotes - statY.Downvotes;
            int rating = -ratingX.CompareTo(ratingY);
            if (rating != 0)
                return rating;
            return ComparePluginsByName(x, y);
        }
    }
}
