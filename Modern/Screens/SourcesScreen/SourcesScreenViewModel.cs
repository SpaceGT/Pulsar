using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Keen.Game2.Client.UI.Library.Dialogs.OneOptionDialog;
using Keen.VRage.UI.Screens;
using Pulsar.Modern.Screens.SourcesScreen.AddRemoteSourceScreen;
using Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;
using Pulsar.Shared;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class SourcesScreenViewModel : ScreenViewModel
{
    public ObservableCollection<SourceViewModel> HubSources { get; private set; } = [];
    public bool HubSourcesEmpty => HubSources.Count == 0;
    public ObservableCollection<SourceViewModel> PluginSources { get; private set; } = [];
    public bool PluginSourcesEmpty => PluginSources.Count == 0;
    public ObservableCollection<SourceViewModel> ModSources { get; private set; } = [];
    public bool ModSourcesEmpty => ModSources.Count == 0;

    // viewmodel | enabled | shouldRemove
    private readonly List<Tuple<SourceViewModel, bool, bool>> sourceChanges = [];

    private SourcesConfig sourcesConfig;

    public SourcesScreenViewModel(SourcesConfig config)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        HubSources.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(HubSourcesEmpty));
        PluginSources.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(PluginSourcesEmpty));
        ModSources.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(ModSourcesEmpty));

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

    public void ApplyChanges()
    {
        List<RemoteHubConfig> remoteHubList = [.. sourcesConfig.RemoteHubSources];
        List<LocalHubConfig> localHubList = [.. sourcesConfig.LocalHubSources];
        List<RemotePluginConfig> remotePluginList = [.. sourcesConfig.RemotePluginSources];
        List<LocalPluginConfig> localPluginList = [.. sourcesConfig.LocalPluginSources];
        List<ModConfig> modList = [.. sourcesConfig.ModSources];

        for (int i = 0; i < sourceChanges.Count; i++)
        {
            if (sourceChanges[i].Item1.IsHub)
            {
                if (sourceChanges[i].Item1.Config is RemoteHubConfig remoteHub)
                {
                    remoteHub.Enabled = sourceChanges[i].Item2;

                    if (sourceChanges[i].Item3)
                        remoteHubList.Remove(remoteHub);
                    else if (!remoteHubList.Contains(remoteHub))
                        remoteHubList.Add(remoteHub);
                }
                else if (sourceChanges[i].Item1.Config is LocalHubConfig localHub)
                {
                    localHub.Enabled = sourceChanges[i].Item2;

                    if (sourceChanges[i].Item3)
                        localHubList.Remove(localHub);
                    else if (!localHubList.Contains(localHub))
                        localHubList.Add(localHub);
                }

                continue;
            }

            if (sourceChanges[i].Item1.IsPlugin)
            {
                if (sourceChanges[i].Item1.Config is RemotePluginConfig remotePlugin)
                {
                    remotePlugin.Enabled = sourceChanges[i].Item2;

                    if (sourceChanges[i].Item3)
                        remotePluginList.Remove(remotePlugin);
                    else if (!remotePluginList.Contains(remotePlugin))
                        remotePluginList.Add(remotePlugin);
                }
                else if (sourceChanges[i].Item1.Config is LocalPluginConfig localPlugin)
                {
                    localPlugin.Enabled = sourceChanges[i].Item2;

                    if (sourceChanges[i].Item3)
                        localPluginList.Remove(localPlugin);
                    else if (!localPluginList.Contains(localPlugin))
                        localPluginList.Add(localPlugin);
                }

                continue;
            }

            var config = sourceChanges[i].Item1.Config as ModConfig;
            config.Enabled = sourceChanges[i].Item2;

            if (sourceChanges[i].Item3)
                modList.Remove(config);
            else if (!modList.Contains(config))
                modList.Add(config);
        }

        sourcesConfig.RemoteHubSources = [.. remoteHubList];
        sourcesConfig.LocalHubSources = [.. localHubList];
        sourcesConfig.RemotePluginSources = [.. remotePluginList];
        sourcesConfig.LocalPluginSources = [.. localPluginList];
        sourcesConfig.ModSources = [.. modList];

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
        sourceChanges.Clear();

        foreach (RemoteHubConfig source in sourcesConfig.RemoteHubSources)
            HubSources.Add(new(source));

        foreach (LocalHubConfig source in sourcesConfig.LocalHubSources)
            HubSources.Add(new(source));

        foreach (RemotePluginConfig source in sourcesConfig.RemotePluginSources)
            PluginSources.Add(new(source));

        foreach (LocalPluginConfig source in sourcesConfig.LocalPluginSources)
            PluginSources.Add(new(source));

        foreach (ModConfig source in sourcesConfig.ModSources)
            ModSources.Add(new(source));
    }

    public void ModifySource(SourceViewModel vm, bool enabled, bool remove)
    {
        if (remove)
        {
            // Collection.Remove() already checks if the object is in the list
            // so we don't need to check if it is in the list

            if (vm.IsHub)
                HubSources.Remove(vm);
            if (vm.IsPlugin)
                PluginSources.Remove(vm);
            else
                ModSources.Remove(vm);
        }
        else
        {
            if (vm.IsHub && !HubSources.Contains(vm))
                HubSources.Add(vm);

            if (vm.IsPlugin && !PluginSources.Contains(vm))
                PluginSources.Add(vm);
            else if (!ModSources.Contains(vm))
                ModSources.Add(vm);
        }

        for (int i = 0; i < sourceChanges.Count; i++)
        {
            if (sourceChanges[i].Item1 == vm)
            {
                sourceChanges[i] = new(vm, enabled, remove);
                return;
            }
        }

        sourceChanges.Add(new(vm, enabled, remove));
    }

    public void OpenDetailsScreen(SourceViewModel vm)
    {
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<SourceInfoScreen.SourceInfoScreen>(
                new SourceInfoScreenViewModel(this, vm),
                true
            );
    }

    public void OpenAddRemoteSourceScreen(
        AddRemoteSourceScreenViewModel.RemoteSourceType sourceType
    )
    {
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<AddRemoteSourceScreen.AddRemoteSourceScreen>(
                new AddRemoteSourceScreenViewModel(this, sourceType),
                true
            );
    }

    public void AddLocalHub()
    {
        Tools.OpenFolderDialog(
            (folder) =>
            {
                bool exists = HubSources.Any(p =>
                    p.Config is LocalHubConfig localHub
                    && string.Equals(localHub.Folder, folder, StringComparison.OrdinalIgnoreCase)
                );
                if (exists)
                {
                    var definition = ScreenTools.GetDefaultOkDialog();
                    definition.Title = ScreenTools.GetKeyFromString("Pulsar");
                    definition.Content = ScreenTools.GetKeyFromString(
                        $"That local hub already exists!"
                    );

                    ScreenTools
                        .GetSharedUIComponent()
                        .ShowDialog(new OneOptionDialogViewModel(definition));
                    return;
                }

                LocalHubConfig hub = new()
                {
                    Name = new DirectoryInfo(folder).Name,
                    Folder = folder,
                    Enabled = true,
                };

                ModifySource(new(hub), true, false);
            }
        );
    }

    public void AddDevFolder()
    {
        Tools.OpenFolderDialog(
            (folder) =>
            {
                if (ConfigManager.Instance.List.Contains(folder))
                {
                    var definition = ScreenTools.GetDefaultOkDialog();
                    definition.Title = ScreenTools.GetKeyFromString("Pulsar");
                    definition.Content = ScreenTools.GetKeyFromString(
                        $"That development folder already exists!"
                    );

                    ScreenTools
                        .GetSharedUIComponent()
                        .ShowDialog(new OneOptionDialogViewModel(definition));
                    return;
                }

                LocalPluginConfig plugin = new()
                {
                    Name = Path.GetFileName(folder),
                    Folder = folder,
                    Enabled = true,
                };
                ModifySource(new(plugin), true, false);
            }
        );
    }

    public void AddCompiledPlugin()
    {
        try
        {
            string localPluginDir = Path.Combine(ConfigManager.Instance.PulsarDir, "Local");
            Directory.CreateDirectory(localPluginDir);
            Process.Start("explorer.exe", $"\"{localPluginDir}\"");
        }
        catch (Exception e)
        {
            LogFile.Error("Error while opening local plugins folder: " + e);
        }
    }

    public void AddRemoteHub(SourceViewModel source)
    {
        if (
            HubSources.Any(
                (x) =>
                    x.Config is RemoteHubConfig remoteHub
                    && remoteHub.Repo == ((RemoteHubConfig)source.Config).Repo
            )
        )
        {
            var definition = ScreenTools.GetDefaultOkDialog();
            definition.Title = ScreenTools.GetKeyFromString("Source Error");
            definition.Content = ScreenTools.GetKeyFromString(
                $"This source already exists in the list."
            );
            definition.ConfirmOption = ScreenTools.GetKeyFromString("Ok");

            ScreenTools.GetSharedUIComponent().ShowDialog(new OneOptionDialogViewModel(definition));
        }
        else
        {
            ModifySource(source, true, false);
        }
    }

    public void AddRemotePlugin(SourceViewModel source)
    {
        if (
            PluginSources.Any(
                (x) =>
                    x.Config is RemotePluginConfig remotePlugin
                    && remotePlugin.Repo == ((RemotePluginConfig)source.Config).Repo
            )
        )
        {
            var definition = ScreenTools.GetDefaultOkDialog();
            definition.Title = ScreenTools.GetKeyFromString("Source Error");
            definition.Content = ScreenTools.GetKeyFromString(
                $"This source already exists in the list."
            );
            definition.ConfirmOption = ScreenTools.GetKeyFromString("Ok");

            ScreenTools.GetSharedUIComponent().ShowDialog(new OneOptionDialogViewModel(definition));
        }
        else
        {
            ModifySource(source, true, false);
        }
    }

    public void AddMod(SourceViewModel source)
    {
        if (ModSources.Any((x) => x.WorkshopId == source.WorkshopId))
        {
            var definition = ScreenTools.GetDefaultOkDialog();
            definition.Title = ScreenTools.GetKeyFromString("Source Error");
            definition.Content = ScreenTools.GetKeyFromString(
                $"This source already exists in the list."
            );
            definition.ConfirmOption = ScreenTools.GetKeyFromString("Ok");

            ScreenTools.GetSharedUIComponent().ShowDialog(new OneOptionDialogViewModel(definition));
        }
        else
        {
            ModifySource(source, true, false);
        }
    }
}
