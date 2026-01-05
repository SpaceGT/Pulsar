using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Shared.Data;
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

            PluginData[] shownPlugins;
            if ((DataContext as AddPluginScreenViewModel).SortMethod == SortingMethod.Search)
                shownPlugins = [.. (DataContext as AddPluginScreenViewModel).Plugins];
            else
                shownPlugins = [.. (DataContext as AddPluginScreenViewModel).Plugins.Where(x => !x.Hidden)];

            PluginList.DataContext = shownPlugins;
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

            List<PluginData> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(dummyPlugin);
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
    }

    private void SearchClearButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
    }
}