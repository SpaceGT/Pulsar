using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.SourcesScreen.AddRemoteSourceScreen;
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

        if (Design.IsDesignMode)
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

    private void AddHubButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) 
    {
        (DataContext as SourcesScreenViewModel).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Hub);
    }

    private void AddLocalHubButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        (DataContext as SourcesScreenViewModel).AddLocalHub();
    }

    private void AddRemotePluginButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        (DataContext as SourcesScreenViewModel).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Plugin);
    }

    private void AddDevFolderButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        (DataContext as SourcesScreenViewModel).AddDevFolder();
    }

    private void AddLocalPluginButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        (DataContext as SourcesScreenViewModel).AddCompiledPlugin();
    }

    private void AddModSourceButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        (DataContext as SourcesScreenViewModel).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Mod);
    }

    private void HubItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) 
    {
        if (selectedHubControl != null)
            (selectedHubControl.Classes as IPseudoClasses).Remove(":selected");

        selectedHubControl = sender as Control;
        (selectedHubControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            (DataContext as SourcesScreenViewModel).OpenDetailsScreen((DataContext as SourcesScreenViewModel).HubSources, selectedHubControl.DataContext);
        }
    }

    private void PluginSourceItem_PointerPressed(
        object sender,
        Avalonia.Input.PointerPressedEventArgs e
    )
    {
        if (selectedPluginControl != null)
            (selectedPluginControl.Classes as IPseudoClasses).Remove(":selected");

        selectedPluginControl = sender as Control;
        (selectedPluginControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            (DataContext as SourcesScreenViewModel).OpenDetailsScreen((DataContext as SourcesScreenViewModel).PluginSources, selectedPluginControl.DataContext);
        }
    }

    private void ModItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) 
    {
        if (selectedModPluginControl != null)
            (selectedModPluginControl.Classes as IPseudoClasses).Remove(":selected");

        selectedModPluginControl = sender as Control;
        (selectedModPluginControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            (DataContext as SourcesScreenViewModel).OpenDetailsScreen((DataContext as SourcesScreenViewModel).ModSources, selectedModPluginControl.DataContext);
        }
    }

    private void HubItemCheckbox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    { 
        if (sender is CheckBox checkBox)
        {
            (DataContext as SourcesScreenViewModel).ModifySource(checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void PluginSourceItemCheckBox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        if (sender is CheckBox checkBox)
        {
            (DataContext as SourcesScreenViewModel).ModifySource(checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void ModSourceCheckbox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        if (sender is CheckBox checkBox)
        {
            (DataContext as SourcesScreenViewModel).ModifySource(checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void ApplyButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) 
    {
        (DataContext as SourcesScreenViewModel).ApplyChanges();
    }

    private void RefreshButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as SourcesScreenViewModel).RefreshSources();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}
