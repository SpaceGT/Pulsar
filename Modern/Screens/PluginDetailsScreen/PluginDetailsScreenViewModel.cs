using Keen.VRage.UI.Screens;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

internal class PluginDetailsScreenViewModel : ScreenViewModel
{
    public PluginViewModel Plugin { get; private set; }

    public PluginDetailsScreenViewModel(PluginViewModel plugin)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        Plugin = plugin;

        InitializeInputContext();
    }
}
