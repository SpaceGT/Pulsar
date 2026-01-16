using Avalonia.Controls;
using Avalonia.Interactivity;
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
    public Profile Draft { get; private set; }

    public List<PluginViewModel> Plugins { get; private set; } = [];
    public List<PluginViewModel> ModPlugins { get; private set; } = [];

    public ObservableCollection<PluginViewModel> EnabledPlugins { get; private set; }
    public ObservableCollection<PluginViewModel> EnabledModPlugins { get; private set; }

    private ConfigManager configManager;

    public readonly PluginList PluginList;
    public readonly ProfilesConfig Profiles;
    public readonly SourcesConfig Sources;

    public bool ConsentGiven = PlayerConsent.ConsentGiven;

    public PluginsScreenViewModel(ConfigManager configManager)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.configManager = configManager;
        Draft = Tools.DeepCopy(this.configManager.Profiles.Current);
        
        foreach (PluginData plugin in this.configManager.List)
        {
            if (plugin is ModPlugin modPlugin)
                ModPlugins.Add(new PluginViewModel(modPlugin, Draft));
            else
                Plugins.Add(new PluginViewModel(plugin, Draft));
        }

        ModPlugins.Sort(ComparePluginsByName);
        Plugins.Sort(ComparePluginsByName);

        EnabledModPlugins = [.. ModPlugins.Where(x => x.DraftEnabled)];
        EnabledPlugins = [.. Plugins.Where(x => x.DraftEnabled)];

        PluginList = this.configManager.List;
        Profiles = this.configManager.Profiles;
        Sources = this.configManager.Sources;

        InitializeInputContext();
    }

    public static void Open()
    {
        var configManager = ConfigManager.Instance;
        PluginsScreenViewModel menu = new(configManager);
        ScreenTools.GetSharedUIComponent().CreateScreen<PluginsScreen>(menu, true);
    }

    public void OnConsentBoxChanged(object sender, RoutedEventArgs e)
    {
        PlayerConsent.ShowDialog();
        UpdateConsentBox((CheckBox)sender);
    }

    public void UpdateConsentBox(CheckBox checkbox)
    {
        if (checkbox.IsChecked != PlayerConsent.ConsentGiven)
        {
            checkbox.IsCheckedChanged -= OnConsentBoxChanged;
            checkbox.IsChecked = PlayerConsent.ConsentGiven;
            checkbox.IsCheckedChanged += OnConsentBoxChanged;
        }
    }

    public void RefreshPluginLists()
    {
        ModPlugins.Clear();
        Plugins.Clear();

        EnabledPlugins.Clear();
        EnabledModPlugins.Clear();

        foreach (PluginData plugin in this.configManager.List)
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

    public bool SyncPluginConfigs()
    {
        Profile current = Profiles.Current;
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

            if (diff && PluginList.TryGetPlugin(id, out PluginData plugin))
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
                && PluginList.TryGetPlugin(configID, out PluginData plugin)
            )
                plugin.LoadData(tFolder);
        }
    }

    // TODO
    public void Save()
    {

    }

    //TODO
    public bool RequiresRestart()
    {
        return true;
    }
}
