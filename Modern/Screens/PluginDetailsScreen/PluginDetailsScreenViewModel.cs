using System;
using Keen.VRage.UI.Screens;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

internal class PluginDetailsScreenViewModel : ScreenViewModel
{
    private readonly Action onScreenClose;
    public PluginViewModel Plugin { get; private set; }

    public PluginDetailsScreenViewModel(PluginViewModel plugin, Action onScreenClose = null)
    {
        this.onScreenClose = onScreenClose;

        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        Plugin = plugin;

        InitializeInputContext();
    }

    public override void OnDispose()
    {
        base.OnDispose();
        onScreenClose?.Invoke();
    }
}
