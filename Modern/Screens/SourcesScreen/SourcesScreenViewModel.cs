using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class SourcesScreenViewModel : ScreenViewModel
{
    public SourcesScreenViewModel(SourcesConfig config)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;



        InitializeInputContext();

    }
}
