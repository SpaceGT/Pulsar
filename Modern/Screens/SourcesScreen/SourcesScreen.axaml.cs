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
            List<SourceViewModel> dummyHubs = [];

            for (int i = 0; i < 25; i++)
            {
                dummyHubs.Add(SourceViewModel.GetDummyHubViewModel());
            }

            HubsList.ItemsSource = dummyHubs;

            List<SourceViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(SourceViewModel.GetDummyPluginViewModel());
            }

            PluginsSourceList.ItemsSource = dummyPlugins;

            List<SourceViewModel> dummyMods = [];

            for (int i = 0; i < 25; i++)
            {
                dummyMods.Add(SourceViewModel.GetDummyModViewModel());
            }

            ModSourceList.ItemsSource = dummyMods;
        }
    }

    private void AddHubButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) 
    {
        ((SourcesScreenViewModel)DataContext).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Hub);
    }

    private void AddLocalHubButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        ((SourcesScreenViewModel)DataContext).AddLocalHub();
    }

    private void AddRemotePluginButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        ((SourcesScreenViewModel)DataContext).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Plugin);
    }

    private void AddDevFolderButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        ((SourcesScreenViewModel)DataContext).AddDevFolder();
    }

    private void AddLocalPluginButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        ((SourcesScreenViewModel)DataContext).AddCompiledPlugin();
    }

    private void AddModSourceButton_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        ((SourcesScreenViewModel)DataContext).OpenAddRemoteSourceScreen(AddRemoteSourceScreenViewModel.RemoteSourceType.Mod);
    }

    private void HubItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) 
    {
        if (selectedHubControl != null)
            ((IPseudoClasses)selectedHubControl.Classes).Remove(":selected");

        selectedHubControl = (Control)sender;
        ((IPseudoClasses)selectedHubControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            ((SourcesScreenViewModel)DataContext).OpenDetailsScreen((SourceViewModel)selectedHubControl.DataContext);
        }
    }

    private void PluginSourceItem_PointerPressed(
        object sender,
        Avalonia.Input.PointerPressedEventArgs e
    )
    {
        if (selectedPluginControl != null)
            ((IPseudoClasses)selectedPluginControl.Classes).Remove(":selected");

        selectedPluginControl = (Control)sender;
        ((IPseudoClasses)selectedPluginControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            ((SourcesScreenViewModel)DataContext).OpenDetailsScreen((SourceViewModel)selectedPluginControl.DataContext);
        }
    }

    private void ModItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) 
    {
        if (selectedModPluginControl != null)
            ((IPseudoClasses)selectedModPluginControl.Classes).Remove(":selected");

        selectedModPluginControl = (Control)sender;
        ((IPseudoClasses)selectedModPluginControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        if (e.ClickCount > 1)
        {
            ((SourcesScreenViewModel)DataContext).OpenDetailsScreen((SourceViewModel)selectedModPluginControl.DataContext);
        }
    }

    private void HubItemCheckbox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    { 
        if (sender is CheckBox checkBox)
        {
            ((SourcesScreenViewModel)DataContext).ModifySource((SourceViewModel)checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void PluginSourceItemCheckBox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        if (sender is CheckBox checkBox)
        {
            ((SourcesScreenViewModel)DataContext).ModifySource((SourceViewModel)checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void ModSourceCheckbox_Click(
        object sender,
        Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        if (sender is CheckBox checkBox)
        {
            ((SourcesScreenViewModel)DataContext).ModifySource((SourceViewModel)checkBox.DataContext, (bool)checkBox.IsChecked, false);
        }
    }

    private void ApplyButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) 
    {
        ((SourcesScreenViewModel)DataContext).ApplyChanges();
        Dispose();
    }

    private void RefreshButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((SourcesScreenViewModel)DataContext).RefreshSources();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}
