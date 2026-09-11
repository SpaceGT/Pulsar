using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.PluginDetailsScreen;
using Pulsar.Shared.Stats;
using static Pulsar.Modern.Screens.AddPluginScreen.AddPluginScreenViewModel;

namespace Pulsar.Modern.Screens.AddPluginScreen;

[NeedsWindowStyles]
public partial class AddPluginScreen : PluginScreenBase
{
    public AddPluginScreen()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            if (((AddPluginScreenViewModel)DataContext).Mods)
                TitleText.Text = "Mod List";

            foreach (SortingMethod sortMethod in Enum.GetValues<SortingMethod>())
            {
                bool usesStats = sortMethod is SortingMethod.Usage or SortingMethod.Rating;
                if (StatsClient.Enabled || !usesStats)
                    SortButton.Items.Add(sortMethod);
            }
        }
        else
        {
            List<PluginViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(PluginViewModel.GetDummyPlugin());
            }

            DataContext = new AddPluginScreenViewModel(dummyPlugins, false);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PluginScrollViewer.Offset.Y != 0 && !string.IsNullOrEmpty(SearchBox.Text))
            PluginScrollViewer.ScrollToHome();

        if (SearchBox.Text != string.Empty)
            SearchClearButton.IsVisible = true;
        else
            SearchClearButton.IsVisible = false;

        SortButton.SelectedItem = SortingMethod.Search;
        ((AddPluginScreenViewModel)DataContext).Filter = SearchBox.Text;
        ((AddPluginScreenViewModel)DataContext).SortPlugins(SortingMethod.Search);
    }

    private void SearchClearButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    private void SortButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortButton.SelectedItem is SortingMethod sortMethod)
            ((AddPluginScreenViewModel)DataContext).SortPlugins(sortMethod);
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void PluginItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (((Border)sender).DataContext is not PluginViewModel pluginVM)
            return;

        ScreenTools.PlayClickSound((Control)sender);

        ScreenTools
            .GetSharedUIComponent()
            .CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(
                new PluginDetailsScreenViewModel(pluginVM),
                true
            );
    }
}
