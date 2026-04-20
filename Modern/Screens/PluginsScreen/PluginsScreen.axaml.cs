using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Loader;
using Pulsar.Modern.Screens.AddPluginScreen;
using Pulsar.Modern.Screens.PluginDetailsScreen;
using Pulsar.Modern.Screens.ProfilesScreen;
using Pulsar.Modern.Screens.SourcesScreen;
using Pulsar.Modern.Screens.SourcesScreen.SourceWarningScreen;
using Pulsar.Shared;

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
            RefreshButton.IsVisible = !Flags.CustomSources;
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

        if (!((PluginsScreenViewModel)DataContext).ApplyChanges())
            return;

        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Title = ScreenTools.GetKeyFromString("Apply Changes?");
        definition.Content = ScreenTools.GetKeyFromString(
            "A restart is required to apply changes. Would you like to restart the game now?"
        );

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TwoOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        LoaderTools.AskToRestart();
                    },
                }
            );
    }

    private void ProfilesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = new ProfilesScreenViewModel(
            ((PluginsScreenViewModel)DataContext).Draft,
            ((PluginsScreenViewModel)DataContext).ReplaceDraft
        );

        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<ProfilesScreen.ProfilesScreen>(viewModel, true);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        ((PluginsScreenViewModel)DataContext).RefreshSources();
        RefreshButton.IsEnabled = false;
    }

    private void PluginAddButton_Click(object sender, RoutedEventArgs e)
    {
        ((PluginsScreenViewModel)DataContext).RefreshPluginLists();

        var viewModel = new AddPluginScreenViewModel(
            [.. ((PluginsScreenViewModel)DataContext).Plugins],
            false,
            delegate()
            {
                ((PluginsScreenViewModel)DataContext).RefreshPluginLists();

                if (selectedPluginControl != null)
                    ((IPseudoClasses)selectedPluginControl.Classes).Remove(":selected");

                if (selectedModPluginControl != null)
                    ((IPseudoClasses)selectedModPluginControl.Classes).Remove(":selected");

                selectedPluginControl = null;
                selectedModPluginControl = null;
            }
        );
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
    }

    private void ModAddButton_Click(object sender, RoutedEventArgs e)
    {
        ((PluginsScreenViewModel)DataContext).RefreshPluginLists();

        var viewModel = new AddPluginScreenViewModel(
            [.. ((PluginsScreenViewModel)DataContext).ModPlugins],
            true,
            delegate()
            {
                ((PluginsScreenViewModel)DataContext).RefreshPluginLists();

                if (selectedPluginControl != null)
                    ((IPseudoClasses)selectedPluginControl.Classes).Remove(":selected");

                if (selectedModPluginControl != null)
                    ((IPseudoClasses)selectedModPluginControl.Classes).Remove(":selected");

                selectedPluginControl = null;
                selectedModPluginControl = null;
            }
        );
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
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

        ((PluginsScreenViewModel)DataContext).ShowConsentScreen();
    }

    private void PluginItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (selectedPluginControl != null)
            ((IPseudoClasses)selectedPluginControl.Classes).Remove(":selected");

        selectedPluginControl = (Control)sender;
        ((IPseudoClasses)selectedPluginControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        ((PluginsScreenViewModel)DataContext).SelectedPlugin = (PluginViewModel)
            ((Control)sender).DataContext;

        PluginSettingsButton.IsEnabled = ((PluginsScreenViewModel)DataContext)
            .SelectedPlugin
            .HasSettingsMenu;
        PluginDetailsButton.IsEnabled = true;

        if (e.ClickCount > 1)
            ScreenTools
                .GetSharedUIComponent()
                .CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(
                    new PluginDetailsScreenViewModel(
                        ((PluginsScreenViewModel)DataContext).SelectedPlugin
                    ),
                    true
                );
    }

    private void ModPluginItem_PointerPressed(
        object sender,
        Avalonia.Input.PointerPressedEventArgs e
    )
    {
        if (selectedModPluginControl != null)
            ((IPseudoClasses)selectedModPluginControl.Classes).Remove(":selected");

        selectedModPluginControl = (Control)sender;
        ((IPseudoClasses)selectedModPluginControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        ((PluginsScreenViewModel)DataContext).SelectedModPlugin = (PluginViewModel)
            ((Control)sender).DataContext;

        ModDetailsButton.IsEnabled = true;

        if (e.ClickCount > 1)
            ScreenTools
                .GetSharedUIComponent()
                .CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(
                    new PluginDetailsScreenViewModel(
                        ((PluginsScreenViewModel)DataContext).SelectedModPlugin
                    ),
                    true
                );
    }

    private void PluginSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ((PluginsScreenViewModel)DataContext).SelectedPlugin.TryOpenSettingsScreen();
    }

    private void PluginDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(
                new PluginDetailsScreenViewModel(
                    ((PluginsScreenViewModel)DataContext).SelectedPlugin
                ),
                true
            );
    }

    private void ModDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(
                new PluginDetailsScreenViewModel(
                    ((PluginsScreenViewModel)DataContext).SelectedModPlugin
                ),
                true
            );
    }

    private void SourcesButton_Click(object sender, RoutedEventArgs e)
    {
        if (((PluginsScreenViewModel)DataContext).Sources.ShowWarning)
            ScreenTools
                .GetSharedUIComponent()
                .CreateScreen<SourceWarningScreen>(
                    new SourceWarningScreenViewModel(
                        ((PluginsScreenViewModel)DataContext).Sources,
                        delegate
                        {
                            ScreenTools
                                .GetSharedUIComponent()
                                .CreateScreen<SourcesScreen.SourcesScreen>(
                                    new SourcesScreenViewModel(
                                        ((PluginsScreenViewModel)DataContext).Sources,
                                        delegate
                                        {
                                            ((PluginsScreenViewModel)DataContext).RefreshPluginLists();
                                        }
                                    ),
                                    true
                                );
                        }
                    ),
                    true
                );
        else
            ScreenTools
                .GetSharedUIComponent()
                .CreateScreen<SourcesScreen.SourcesScreen>(
                    new SourcesScreenViewModel(
                        ((PluginsScreenViewModel)DataContext).Sources,
                        delegate
                        {
                            ((PluginsScreenViewModel)DataContext).RefreshPluginLists();
                        }),
                    true
                );
    }
}
