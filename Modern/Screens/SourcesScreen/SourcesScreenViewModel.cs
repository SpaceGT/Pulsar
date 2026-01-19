using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using System.Collections.ObjectModel;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class SourcesScreenViewModel : ScreenViewModel
{
    public ObservableCollection<HubSourceViewModel> HubSources { get; private set; } = [];
    public ObservableCollection<HubSourceViewModel> PluginSources { get; private set; } = [];
    public ObservableCollection<HubSourceViewModel> ModSources { get; private set; } = [];

    private readonly SourcesConfig sourcesConfig;
    public SourcesScreenViewModel(SourcesConfig config)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        sourcesConfig = config;

        InitializeInputContext();

    }
}
