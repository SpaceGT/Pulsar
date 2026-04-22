using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using Keen.VRage.UI.Screens;

namespace Pulsar.Modern.Screens.AddPluginScreen;

internal class AddPluginScreenViewModel : ScreenViewModel
{
    public ObservableCollection<PluginViewModel> Plugins { get; private set; }
    public readonly bool Mods;
    public string Filter;
    public SortingMethod SortMethod = SortingMethod.Name;

    private readonly List<PluginViewModel> plugins;
    private event Action onScreenClose;

    public enum SortingMethod : int
    {
        Name,
        Search,
        Usage,
        Rating,
    }

    public AddPluginScreenViewModel(List<PluginViewModel> plugins, bool mods, Action onScreenClose = null)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        InitializeInputContext();

        Mods = mods;

        this.plugins = plugins;
        Plugins = new([.. this.plugins.Where(x => !x.IsHidden && x.IsSupportedRuntime)]);
        this.onScreenClose = onScreenClose;

        SortPlugins(SortingMethod.Name);
    }

    public override void OnDispose()
    {
        base.OnDispose();
        onScreenClose?.Invoke();
    }

    public void SortPlugins(SortingMethod sort)
    {
        switch (sort)
        {
            case SortingMethod.Name:
                plugins.Sort(ComparePluginsByName);
                break;
            case SortingMethod.Usage:
                plugins.Sort(ComparePluginsByUsage);
                break;
            case SortingMethod.Rating:
                plugins.Sort(ComparePluginsByRating);
                break;
            case SortingMethod.Search:
                SortPluginsBySearch();
                break;
            default:
                plugins.Sort(ComparePluginsByName);
                break;
        }

        Plugins.Clear();

        Plugins.AddRange([
            .. plugins.Where(x =>
                (!x.IsHidden || x.FriendlyName.Equals(Filter, StringComparison.OrdinalIgnoreCase))
                && x.IsSupportedRuntime
            ),
        ]);
    }

    public void SortPluginsBySearch()
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return;

        var scoreCache = plugins.ToDictionary(p => p, p => p.Rank(Filter));
        plugins.Sort(Comparator);

        int Comparator(PluginViewModel x, PluginViewModel y)
        {
            int comp = scoreCache[y].CompareTo(scoreCache[x]);
            return comp == 0 ? ComparePluginsByName(x, y) : comp;
        }
    }

    private int ComparePluginsByName(PluginViewModel x, PluginViewModel y)
    {
        return x.FriendlyName.CompareTo(y.FriendlyName, StringComparison.OrdinalIgnoreCase);
    }

    private int ComparePluginsByUsage(PluginViewModel x, PluginViewModel y)
    {
        int usage = -x.Players.CompareTo(y.Players);
        if (usage != 0)
            return usage;
        return ComparePluginsByName(x, y);
    }

    private int ComparePluginsByRating(PluginViewModel x, PluginViewModel y)
    {
        int ratingX = x.Upvotes - x.Downvotes;
        int ratingY = y.Upvotes - y.Downvotes;
        int rating = -ratingX.CompareTo(ratingY);
        if (rating != 0)
            return rating;
        return ComparePluginsByName(x, y);
    }
}
