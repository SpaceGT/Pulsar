using DynamicData;
using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pulsar.Modern.Screens.PluginsScreen;

internal class PluginsScreenViewModel : ScreenViewModel
{
    public List<PluginViewModel> Plugins { get; private set; } = [];
    public List<PluginViewModel> ModPlugins { get; private set; } = [];

    public ObservableCollection<PluginViewModel> EnabledPlugins { get; private set; } = [];
    public ObservableCollection<PluginViewModel> EnabledModPlugins { get; private set; } = [];

    public Profile Draft { get; private set; }

    public bool ConsentGiven => configManager.Core.DataHandlingConsent;

    private readonly ConfigManager configManager;
    private readonly ProfilesConfig profiles;
    
    private readonly PluginList pluginlist;

    public PluginsScreenViewModel(ConfigManager configManager)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.configManager = configManager;
        profiles = this.configManager.Profiles;
        Draft = Tools.DeepCopy(this.configManager.Profiles.Current);
        pluginlist = this.configManager.List;

        RefreshPluginLists();

        InitializeInputContext();
    }

    public static void OpenMenu()
    {
        var configManager = ConfigManager.Instance;
        PluginsScreenViewModel menu = new(configManager);
        ScreenTools.GetSharedUIComponent().CreateScreen<PluginsScreen>(menu, true);
    }

    public void RefreshPluginLists()
    {
        ModPlugins.Clear();
        Plugins.Clear();

        EnabledPlugins.Clear();
        EnabledModPlugins.Clear();

        foreach (PluginData plugin in pluginlist)
        {
            if (plugin is ModPlugin modPlugin)
                ModPlugins.Add(new PluginViewModel(modPlugin, Draft));
            else
                Plugins.Add(new PluginViewModel(plugin, Draft));
        }

        ModPlugins.Sort(ComparePluginsByName);
        Plugins.Sort(ComparePluginsByName);

        EnabledModPlugins.AddRange([.. ModPlugins.Where(x => x.DraftEnabled)]);
        EnabledPlugins.AddRange([.. Plugins.Where(x => x.DraftEnabled)]);
    }

    private int ComparePluginsByName(PluginViewModel x, PluginViewModel y)
    {
        return x.FriendlyName.CompareTo(y.FriendlyName, StringComparison.OrdinalIgnoreCase);
    }

    public void ReplaceDraft(Profile profile)
    {
        SyncDevFolders(profile, Draft);
        profile.Name = Draft.Name;
        Draft = profile;

        RefreshPluginLists();
    }

    public void RefreshSources()
    {
        pluginlist.UpdateRemoteList();
        pluginlist.UpdateLocalList();
        configManager.Sources.Save();
    }

    public void ShowConsentScreen() => PlayerConsent.ShowDialog(() => OnPropertyChanged(nameof(ConsentGiven)));

    public bool ApplyChanges()
    {
        if (!SyncPluginConfigs())
            return false;

        foreach (string id in Draft.GetPluginIDs())
            pluginlist.SubscribeToItem(id);

        profiles.Current = Draft;
        profiles.Save();

        return true;
    }

    public bool SyncPluginConfigs()
    {
        Profile current = profiles.Current;
        bool hasDiff = false;

        foreach (string id in current.GetPluginIDs().Concat(Draft.GetPluginIDs()))
        {
            PluginDataConfig cConfig = current.GetData(id);
            PluginDataConfig dConfig = Draft.GetData(id);

            // Prebuilt and Mod plugins lack a config
            // FIXME: The diff check would have "just worked" if they did
            if (cConfig is null && dConfig is null)
            {
                hasDiff |= current.Local.Contains(id) != Draft.Local.Contains(id);

                if (ulong.TryParse(id, out ulong wId))
                    hasDiff |= current.Mods.Contains(wId) != Draft.Mods.Contains(wId);

                continue;
            }

            bool diff = cConfig is null || dConfig is null;

            if (cConfig is GitHubPluginConfig cGitHub && dConfig is GitHubPluginConfig dGitHub)
                diff |= cGitHub.SelectedVersion != dGitHub.SelectedVersion;

            if (cConfig is LocalFolderConfig cFolder && dConfig is LocalFolderConfig dFolder)
                diff |=
                    cFolder.DataFile != dFolder.DataFile
                    || cFolder.DebugBuild != dFolder.DebugBuild;

            if (diff && pluginlist.TryGetPlugin(id, out PluginData plugin))
                plugin.LoadData(dConfig);

            hasDiff |= diff;
        }

        return hasDiff;
    }

    private void SyncDevFolders(Profile target, Profile previous)
    {
        IEnumerable<string> folderIDs = target
            .DevFolder.Concat(previous.DevFolder)
            .Select(c => c.Id);

        foreach (string configID in folderIDs)
        {
            var tFolder = (LocalFolderConfig)target.GetData(configID);
            var pFolder = (LocalFolderConfig)previous.GetData(configID);

            if (
                tFolder?.DataFile != pFolder?.DataFile
                && pluginlist.TryGetPlugin(configID, out PluginData plugin)
            )
                plugin.LoadData(tFolder);
        }
    }
}
