using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.PluginDetailsScreen;
using Pulsar.Modern.Screens.PluginsScreen;
using Pulsar.Modern.Screens.ProfilesScreen;
using Pulsar.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if ((DataContext as AddPluginScreenViewModel).Mods)
                TitleText.Text = "Mod List";

            RefreshPluginList();

            SortButton.PlaceholderText = "Sort by";
            string[] sortMethods = Enum.GetNames(typeof(SortingMethod));
            for (int i = 0; i < sortMethods.Length; i++)
                SortButton.Items.Add(sortMethods[i]);
        }
        else
        {
            PluginData dummyPlugin = new GitHubPlugin()
            {
                FriendlyName = "TEST PLUGIN",
                Author = "A user",
                Status = PluginStatus.Updated,
                Tooltip = "TEST MOD DESCRIPTION\nTEST MOD DESCRIPTION\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nLine4\nLine5",
                Source = "Local"
            };

            List<PluginViewModel> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(new PluginViewModel(dummyPlugin, true));
            }

            PluginList.DataContext = dummyPlugins;
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text != string.Empty)
            SearchClearButton.IsVisible = true;
        else
            SearchClearButton.IsVisible = false;

        SortButton.SelectedIndex = (int)SortingMethod.Search;
        (DataContext as AddPluginScreenViewModel).Filter = SearchBox.Text;
        (DataContext as AddPluginScreenViewModel).SortPluginsBySearch();
        RefreshPluginList();
    }

    private void SearchClearButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }

    private void PluginList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        PluginViewModel pluginVM = (PluginViewModel)PluginList.SelectedItem;

        if (pluginVM == null)
            return;

        var viewModel = new PluginDetailsScreenViewModel(pluginVM, (DataContext as AddPluginScreenViewModel).Draft);
        viewModel.OnScreenClose += () => RefreshPluginList();

        ScreenTools.GetSharedUIComponent().CreateScreen<PluginDetailsScreen.PluginDetailsScreen>(viewModel, true);
    }

    private void SortButton_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SortingMethod selectedItem = (SortingMethod)SortButton.SelectedIndex;
        (DataContext as AddPluginScreenViewModel).SortPlugins(selectedItem);
        RefreshPluginList();
    }

    private async void RefreshPluginList()
    {
        PluginList.ItemsSource = null;

        PluginViewModel[] shownPlugins;
        if ((DataContext as AddPluginScreenViewModel).SortMethod == SortingMethod.Search)
            PluginList.ItemsSource = (DataContext as AddPluginScreenViewModel).Plugins;
        else
            PluginList.ItemsSource = (DataContext as AddPluginScreenViewModel).Plugins.Where(x => !x.PluginData.Hidden);

        PluginList.ItemsSource = (DataContext as AddPluginScreenViewModel).Hidden
            .Where(x => x.PluginData.FriendlyName.Equals((DataContext as AddPluginScreenViewModel).Filter, StringComparison.OrdinalIgnoreCase))
            .Concat((DataContext as AddPluginScreenViewModel).Plugins)
            .ToArray();
    }


    private void PluginEnabledCheckbox_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as CheckBox).DataContext is not PluginViewModel plugin)
            return;

        plugin.PluginData.UpdateProfile((DataContext as AddPluginScreenViewModel).Draft, (bool)(sender as CheckBox).IsChecked);

        if (!(bool)(sender as CheckBox).IsChecked && plugin.PluginData is LocalFolderPlugin devFolder)
            devFolder.DeserializeFile(null);
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }
}