using Avalonia.Controls;
using Avalonia.Interactivity;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.AddPluginScreen;
using Pulsar.Modern.Screens.PluginDetailsScreen;
using Pulsar.Modern.Screens.ProfilesScreen;
using Pulsar.Modern.Screens.SourcesScreen.SourceWarningScreen;
using Pulsar.Shared;
using System.Collections.Generic;

namespace Pulsar.Modern.Screens.PluginsScreen;

[NeedsWindowStyles]
public partial class PluginsScreen : PluginScreenBase
{
    private Control selectedPluginControl;

    private Control selectedModPluginControl;

    public PluginsScreen()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            SourcesButton.IsVisible = Flags.CustomSources;
        }
        else
        {
            List<PluginViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(PluginViewModel.GetDummyPlugin());
            }

            PluginsList.ItemsSource = dummyPlugins;
            ModsList.ItemsSource = dummyPlugins;

            SourcesButton.IsVisible = Flags.CustomSources;
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void ApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        Dispose();

        if (!(DataContext as PluginsScreenViewModel).ApplyChanges())
            return;

        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Title = ScreenTools.GetKeyFromString("Apply Changes?");
        definition.Content = ScreenTools.GetKeyFromString("A restart is required to apply changes. Would you like to restart the game now?");

        ScreenTools.GetSharedUIComponent().ShowDialog(new TwoOptionsDialogViewModel(definition)
        {
            ConfirmAction = () =>
            {


            }
        });

    }

    private void ProfilesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = new ProfilesScreenViewModel((DataContext as PluginsScreenViewModel).Draft, (DataContext as PluginsScreenViewModel).ReplaceDraft);

        ScreenTools.GetSharedUIComponent().CreateScreen<ProfilesScreen.ProfilesScreen>(viewModel, true);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as PluginsScreenViewModel).RefreshSources();
        RefreshButton.IsEnabled = false;
    }

    private void PluginAddButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new AddPluginScreenViewModel([.. (DataContext as PluginsScreenViewModel).Plugins], false, delegate ()
        {
            (DataContext as PluginsScreenViewModel).RefreshPluginLists();
        });
        ScreenTools.GetSharedUIComponent().CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
    }

    private void ModAddButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new AddPluginScreenViewModel([.. (DataContext as PluginsScreenViewModel).ModPlugins], true, delegate ()
        {
            (DataContext as PluginsScreenViewModel).RefreshPluginLists();
        });
        ScreenTools.GetSharedUIComponent().CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
    }

    private void ConsentBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        // This is to maintain the state of the checkbox as it mainly acts more like a indicator and button.

        if (checkBox.IsChecked.Value)
            checkBox.IsChecked = false;
        else
            checkBox.IsChecked = true;

        (DataContext as PluginsScreenViewModel).ShowConsentScreen();
    }

    private void PluginItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (selectedPluginControl != null)
            (selectedPluginControl.Classes as IPseudoClasses).Remove(":selected");

        selectedPluginControl = sender as Control;
        (selectedPluginControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        (DataContext as PluginsScreenViewModel).SelectedPlugin = (PluginViewModel)(sender as Control).DataContext;

        PluginSettingsButton.IsEnabled = (DataContext as PluginsScreenViewModel).SelectedPlugin.HasSettingsMenu;
        PluginDetailsButton.IsEnabled = true;

        if (e.ClickCount > 1)
            ScreenTools.GetSharedUIComponent().CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(new PluginDetailsScreenViewModel((DataContext as PluginsScreenViewModel).SelectedPlugin), true);
    }

    private void ModPluginItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (selectedModPluginControl != null)
            (selectedModPluginControl.Classes as IPseudoClasses).Remove(":selected");

        selectedModPluginControl = sender as Control;
        (selectedModPluginControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        (DataContext as PluginsScreenViewModel).SelectedModPlugin = (PluginViewModel)(sender as Control).DataContext;

        ModDetailsButton.IsEnabled = true;

        if (e.ClickCount > 1)
            ScreenTools.GetSharedUIComponent().CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(new PluginDetailsScreenViewModel((DataContext as PluginsScreenViewModel).SelectedModPlugin), true);
    }

    private void PluginSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as PluginsScreenViewModel).SelectedPlugin.TryOpenSettingsScreen();
    }

    private void PluginDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        ScreenTools.GetSharedUIComponent().CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(new PluginDetailsScreenViewModel((DataContext as PluginsScreenViewModel).SelectedPlugin), true);
    }

    private void ModDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        ScreenTools.GetSharedUIComponent().CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(new PluginDetailsScreenViewModel((DataContext as PluginsScreenViewModel).SelectedModPlugin), true);
    }

    private void SourcesButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DataContext as PluginsScreenViewModel).Sources.ShowWarning)
            ScreenTools.GetSharedUIComponent().CreateScreen<SourceWarningScreen>(new SourceWarningScreenViewModel((DataContext as PluginsScreenViewModel).Sources, delegate
            {

            }), true);
    }
}