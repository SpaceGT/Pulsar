using Avalonia.Controls;
using Avalonia.Interactivity;
using Keen.Game2.Game.Plugins;
using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Modern.Screens.PluginsScreen;

internal class PluginsScreenViewModel : ScreenViewModel
{
    public Profile Draft { get; private set; }

    private ConfigManager configManager;

    public readonly PluginList PluginList;
    public readonly ProfilesConfig Profiles;
    public readonly SourcesConfig Sources;

    public bool ConsentGiven = PlayerConsent.ConsentGiven;

    public event Action OnListRefreshed;

    public PluginsScreenViewModel(ConfigManager configManager)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.configManager = configManager;
        Draft = Tools.DeepCopy(this.configManager.Profiles.Current);
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
        OnListRefreshed?.Invoke();
    }

    public void ReplaceDraft(Profile profile)
    {
        SyncDevFolders(profile, Draft);
        profile.Name = Draft.Name;
        Draft = profile;
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
