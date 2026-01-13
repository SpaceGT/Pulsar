using Avalonia.Controls;
using Avalonia.Interactivity;
using Keen.Game2.Client.UI.Library.Dialogs.ThreeOptionsDialog;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.AddPluginScreen;
using Pulsar.Modern.Screens.ProfilesScreen;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Modern.Screens.PluginsScreen;

[NeedsWindowStyles]
public partial class PluginsScreen : PluginScreenBase
{
    public PluginsScreen()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            (DataContext as PluginsScreenViewModel).OnListRefreshed += delegate
            {
                List<PluginViewModel> vms = [];

                foreach (PluginData plugin in (DataContext as PluginsScreenViewModel).PluginList.OrderBy(x => x.FriendlyName))
                {
                    if (!(DataContext as PluginsScreenViewModel).Draft.Contains(plugin.Id))
                        continue;

                    if (plugin is ModPlugin)
                        continue;

                    var tip = plugin.FriendlyName;
                    if (!string.IsNullOrWhiteSpace(plugin.Tooltip))
                        tip += "\n" + plugin.Tooltip;

                    PluginViewModel vm = new PluginViewModel(plugin, true)
                    {
                        StatusString = ConfigManager.Instance.SafeMode ? "Disabled" : plugin.StatusString,
                        VersionString = plugin.Version?.ToString() ?? "N/A",
                        ToolTipString = tip
                    };

                    vms.Add(vm);
                }

                PluginsList.DataContext = vms;
            };

            (DataContext as PluginsScreenViewModel).RefreshPluginLists();

            ConsentBox.IsChecked = (DataContext as PluginsScreenViewModel).ConsentGiven;
            ConsentBox.IsCheckedChanged += (DataContext as PluginsScreenViewModel).OnConsentBoxChanged;
            PlayerConsent.OnConsentChanged += OnConsentChanged;

            SourcesButton.IsVisible = Flags.CustomSources;
        }
        else
        {
            PluginData dummyPlugin = new GitHubPlugin()
            {
                FriendlyName = "TEST PLUGIN",
                Author = "A user",
                Status = PluginStatus.Updated
            };

            List<PluginData> dummyPlugins = [];

            for (int i = 0; i < 25; i++)
            {
                dummyPlugins.Add(dummyPlugin);
            }

            PluginsList.DataContext = dummyPlugins;
        }
    }

    public override void OnDispose()
    {
        base.OnDispose();
        PlayerConsent.OnConsentChanged -= OnConsentChanged;
    }

    private void OnConsentChanged()
    {
        (DataContext as PluginsScreenViewModel).UpdateConsentBox(ConsentBox);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((DataContext as PluginsScreenViewModel).RequiresRestart())
        {
            var definition = ScreenTools.GetDefaultYesNoCancelDialog();
            definition.Title = ScreenTools.GetKeyFromString("Apply Changes?");
            definition.Content = ScreenTools.GetKeyFromString("A restart is required to apply changes. Would you like to restart the game now?");

            ScreenTools.GetSharedUIComponent().ShowDialog(new ThreeOptionsDialogViewModel(definition)
            {
                ConfirmAction = () =>
                {
                    (DataContext as PluginsScreenViewModel).Save();
                    //LoaderTools.AskToRestart();
                },
                DefaultAction = () =>
                {
                    (DataContext as PluginsScreenViewModel).Save();
                    Dispose();
                }
            });
        }
        else
        {
            Dispose();
        }
    }

    private void ProfilesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = new ProfilesScreenViewModel((DataContext as PluginsScreenViewModel).Draft);
        viewModel.OnDraftChange += (DataContext as PluginsScreenViewModel).ReplaceDraft;
        viewModel.OnScreenClose += () => (DataContext as PluginsScreenViewModel).RefreshPluginLists();

        ScreenTools.GetSharedUIComponent().CreateScreen<ProfilesScreen.ProfilesScreen>(viewModel, true);
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as PluginsScreenViewModel).PluginList.UpdateRemoteList();
        (DataContext as PluginsScreenViewModel).PluginList.UpdateLocalList();
        (DataContext as PluginsScreenViewModel).Sources.Save();

        RefreshButton.IsEnabled = false;
    }

    private void PluginAddButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = new AddPluginScreenViewModel((DataContext as PluginsScreenViewModel).PluginList, false, (DataContext as PluginsScreenViewModel).Draft);
        viewModel.OnScreenClose += () => (DataContext as PluginsScreenViewModel).RefreshPluginLists();

        ScreenTools.GetSharedUIComponent().CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
    }

    private void ModAddButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = new AddPluginScreenViewModel((DataContext as PluginsScreenViewModel).PluginList, true, (DataContext as PluginsScreenViewModel).Draft);
        viewModel.OnScreenClose += () => (DataContext as PluginsScreenViewModel).RefreshPluginLists();

        ScreenTools.GetSharedUIComponent().CreateScreen<AddPluginScreen.AddPluginScreen>(viewModel, true);
    }
}