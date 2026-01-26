using Keen.VRage.UI.Screens;
using Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class SourcesScreenViewModel : ScreenViewModel
{
    public ObservableCollection<HubSourceViewModel> HubSources { get; private set; } = [];
    public bool HubSourcesEmpty => HubSources.Count == 0;
    public ObservableCollection<PluginSourceViewModel> PluginSources { get; private set; } = [];
    public bool PluginSourcesEmpty => PluginSources.Count == 0;
    public ObservableCollection<ModSourceViewModel> ModSources { get; private set; } = [];
    public bool ModSourcesEmpty => ModSources.Count == 0;


    private readonly Dictionary<object, bool> enabledSourceChanges = [];

    private readonly List<object> sourcesToRemove = [];

    private SourcesConfig sourcesConfig;

    public SourcesScreenViewModel(SourcesConfig config)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        sourcesConfig = config;

        RefreshSourcesLists();

        InitializeInputContext();
    }

    public void RefreshSources()
    {
        // FIXME: Syncronise working copy and with real sources before refreshing
        ConfigManager.Instance.List.UpdateRemoteList(force: true);
        ConfigManager.Instance.List.UpdateLocalList();

        sourcesConfig.Save();

        RefreshSourcesLists();
    }

    private void RefreshSourcesLists()
    {
        HubSources.Clear();
        PluginSources.Clear();
        ModSources.Clear();

        foreach (RemoteHubConfig source in sourcesConfig.RemoteHubSources)
        {
            HubSources.Add(new HubSourceViewModel(source));
        }

        foreach (LocalHubConfig source in sourcesConfig.LocalHubSources)
        {
            HubSources.Add(new HubSourceViewModel(source));
        }

        foreach (RemotePluginConfig source in sourcesConfig.RemotePluginSources)
        {
            PluginSources.Add(new PluginSourceViewModel(source));
        }

        foreach (LocalPluginConfig source in sourcesConfig.LocalPluginSources)
        {
            PluginSources.Add(new PluginSourceViewModel(source));
        }

        foreach (ModConfig source in sourcesConfig.ModSources)
        {
            ModSources.Add(new ModSourceViewModel(source));
        }
    }

    public void OpenDetailsScreen(object vm)
    {
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<SourceInfoScreen.SourceInfoScreen>(new SourceInfoScreenViewModel(sourcesToRemove, vm), true);
    }
}
