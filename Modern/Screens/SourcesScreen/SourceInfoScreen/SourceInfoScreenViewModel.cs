using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using System;
namespace Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;

internal class SourceInfoScreenViewModel : ScreenViewModel
{
    public string SourceInfoText
    {
        get
        {
            if (sourceViewModel.IsHub)
                return GetHubInfoText(sourceViewModel);

            if (sourceViewModel.IsPlugin)
                return GetPluginInfoText(sourceViewModel);

            return GetModInfoText(sourceViewModel);
        }
    }

    public string SourceName => sourceViewModel.Name;

    private readonly SourceViewModel sourceViewModel;
    
    private readonly SourcesScreenViewModel sourceScreenVm;

    public SourceInfoScreenViewModel(SourcesScreenViewModel vm, SourceViewModel sourceVm) 
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        sourceViewModel = sourceVm;
        sourceScreenVm = vm;

        InitializeInputContext();
    }

    public void RemoveSource()
    {
        sourceScreenVm.ModifySource(sourceViewModel, false, true);
    }

    private string GetHubInfoText(SourceViewModel hubVm)
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

    private string GetPluginInfoText(SourceViewModel pluginVm)
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

    private string GetModInfoText(SourceViewModel modVm)
    {
        string modInfoText = string.Empty;

        modInfoText += $"Name: {modVm.Name}\n";
        modInfoText += $"Id: {modVm.WorkshopId}\n";
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
