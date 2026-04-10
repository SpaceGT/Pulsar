using Avalonia.Controls;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using System;

namespace Pulsar.Modern.Screens.PluginDetailsScreen.Controls;

public partial class GithubPluginControls : UserControl
{
    public GithubPluginControls()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData is not GitHubPlugin)
            return;

        if (((GitHubPlugin)((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData).AlternateVersions is null)
            return;

        foreach (var item in ((GitHubPlugin)((PluginDetailsScreenViewModel)DataContext).Plugin.PluginData).AlternateVersions)
        {
            VersionSelectorBox.Items.Add(item.Name);
            if (item.Name == ((GitHubPluginConfig)((PluginDetailsScreenViewModel)DataContext).Plugin.PluginConfig)?.SelectedVersion)
                VersionSelectorBox.SelectedItem = item;
        }
    }

    private void VersionSelectorBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ((GitHubPluginConfig)((PluginDetailsScreenViewModel)DataContext).Plugin.PluginConfig)?.SelectedVersion = (string)VersionSelectorBox.SelectedItem;
    }
}