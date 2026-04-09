using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.PluginDetailsScreen;
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

            string[] sortMethods = Enum.GetNames<SortingMethod>();
            for (int i = 0; i < sortMethods.Length; i++)
                SortButton.Items.Add(sortMethods[i]);
        }
        else
        {
            List<PluginViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(PluginViewModel.GetDummyPlugin());
            }

            DataContext = new AddPluginScreenViewModel(dummyPlugins, false, null);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text != string.Empty)
            SearchClearButton.IsVisible = true;
        else
            SearchClearButton.IsVisible = false;

        SortButton.SelectedIndex = (int)SortingMethod.Search;
        ((AddPluginScreenViewModel)DataContext).Filter = SearchBox.Text;
        ((AddPluginScreenViewModel)DataContext).SortPlugins(SortingMethod.Search);
    }

    private void SearchClearButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    private void SortButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ((AddPluginScreenViewModel)DataContext).SortPlugins(
            (SortingMethod)SortButton.SelectedIndex
        );
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
