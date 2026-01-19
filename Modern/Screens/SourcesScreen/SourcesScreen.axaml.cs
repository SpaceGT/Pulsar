using Avalonia.Controls;
using DynamicData;
using Keen.VRage.UI.AvaloniaInterface.Services;
using System.Collections.Generic;

namespace Pulsar.Modern.Screens.SourcesScreen;

[NeedsWindowStyles]
public partial class SourcesScreen : PluginScreenBase
{
    private Control selectedHubControl;
    private Control selectedPluginControl;
    private Control selectedModPluginControl;
    public SourcesScreen()
    {
        InitializeComponent();

        if (true)
        {
            List<HubSourceViewModel> dummyHubs = [];

            for (int i = 0; i < 25; i++)
            {
                dummyHubs.Add(HubSourceViewModel.GetDummyViewModel());
            }

            HubsList.ItemsSource = dummyHubs;

            List<PluginSourceViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(PluginSourceViewModel.GetDummyViewModel());
            }

            PluginsSourceList.ItemsSource = dummyPlugins;

            List<ModSourceViewModel> dummyMods = [];

            for (int i = 0; i < 25; i++)
            {
                dummyMods.Add(ModSourceViewModel.GetDummyViewModel());
            }

            ModSourceList.ItemsSource = dummyMods;
        }
    }

    private void HubItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
    }

    private void AddHubButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void AddLocalHubButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void PluginSourceItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
    }

    private void AddRemotePluginButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void AddDevFolderButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void AddLocalPluginButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ModItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
    }

    private void AddModSourceButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void HubItemCheckbox_IsCheckedChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void PluginSourceItemCheckBox_IsCheckedChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ModSourceCheckbox_IsCheckedChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ApplyButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void RefreshButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}