using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;

internal class SourceInfoScreenViewModel : ScreenViewModel
{
    public string SourceInfoText
    {
        get
        {
            if (sourceViewModel is HubSourceViewModel hubVm)
                return GetHubInfoText(hubVm);

            if (sourceViewModel is PluginSourceViewModel pluginVm)
                return GetPluginInfoText(pluginVm);

            if (sourceViewModel is ModSourceViewModel modVm)
                return GetModInfoText(modVm);

            return null;
        }
    }

    public string SourceName
    {
        get
        {
            if (sourceViewModel is HubSourceViewModel hubVm)
                return hubVm.Name;

            if (sourceViewModel is PluginSourceViewModel pluginVm)
                return pluginVm.Name;

            if (sourceViewModel is ModSourceViewModel modVm)
                return modVm.Name;

            return null;
        }
    }

    private readonly object sourceViewModel;
    private readonly object displayList;
    private readonly List<object> removeList;

    public SourceInfoScreenViewModel(object displayList, List<object> removeList, object sourceVm) 
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        sourceViewModel = sourceVm;
        this.displayList = displayList;
        this.removeList = removeList;

        InitializeInputContext();
    }

    public void RemoveSource()
    {
        if (displayList is ObservableCollection<HubSourceViewModel> hubs)
            hubs.Remove((HubSourceViewModel)sourceViewModel);

        if (displayList is ObservableCollection<PluginSourceViewModel> plugins)
            plugins.Remove((PluginSourceViewModel)sourceViewModel);

        if (sourceViewModel is ObservableCollection<ModSourceViewModel> mods)
            mods.Remove((ModSourceViewModel)sourceViewModel);

        removeList.Add(sourceViewModel);
    }

    private string GetHubInfoText(HubSourceViewModel hubVm)
    {
        string hubInfoText = string.Empty;

        hubInfoText += $"Name: {hubVm.Name}\n";

        if (hubVm.Config is RemoteHubConfig remoteCfg)
        {
            hubInfoText += $"Repo: {remoteCfg.Repo}\n";
            hubInfoText += $"Branch: {remoteCfg.Branch}\n";
            hubInfoText += $"Last Check: {DateToString(remoteCfg.LastCheck)}\n";
        }
        else if (hubVm.Config is LocalHubConfig localCfg)
            hubInfoText += $"Folder: {localCfg.Folder}\n";

        hubInfoText += $"Hash: {hubVm.Hash ?? "Unknown"}\n";
        hubInfoText += $"Enabled: {hubVm.IsEnabled}\n";
        
        if (hubVm.Config is RemoteHubConfig remoteHub)
            hubInfoText += $"Official: {remoteHub.Trusted}\n";

        return hubInfoText;
    }

    private string GetPluginInfoText(PluginSourceViewModel pluginVm)
    {
        string pluginInfoText = string.Empty;

        pluginInfoText += $"Name: {pluginVm.Name}\n";

        if (pluginVm.Config is RemotePluginConfig remoteCfg)
        {
            pluginInfoText += $"Repo: {remoteCfg.Repo}\n";
            pluginInfoText += $"Branch: {remoteCfg.Branch}\n";
            pluginInfoText += $"Last Check: {DateToString(remoteCfg.LastCheck)}\n";
        }
        else if (pluginVm.Config is LocalPluginConfig localCfg)
            pluginInfoText += $"Folder: {localCfg.Folder}\n";

        pluginInfoText += $"Enabled: {pluginVm.IsEnabled}\n";

        if (pluginVm.Config is RemotePluginConfig remotePlugin)
            pluginInfoText += $"Official: {remotePlugin.Trusted}\n";

        return pluginInfoText;
    }

    private string GetModInfoText(ModSourceViewModel modVm)
    {
        string modInfoText = string.Empty;

        modInfoText += $"Name: {modVm.Name}\n";
        modInfoText += $"Id: {modVm.Id}\n";
        modInfoText += $"Enabled: {modVm.IsEnabled}\n";

        return modInfoText;
    }

    private string DateToString(DateTime? dateTime)
    {
        if (dateTime is DateTime dt)
            return dt.ToLocalTime().ToString("HH:mm:ss yyyy-MM-dd");

        return "Never";
    }
}
