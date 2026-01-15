using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Modern.Loader;
using Pulsar.Shared.Data;
using System;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

internal class PluginDetailsScreenViewModel : ScreenViewModel
{
    public PluginViewModel Plugin { get; private set; }
    public PluginInstance PluginInstance { get; private set; }
    public readonly Profile Draft;

    public event Action OnScreenClosed;

    public PluginDetailsScreenViewModel(PluginViewModel plugin, Profile draft)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.Plugin = plugin;
        this.Draft = draft;

        if (!Design.IsDesignMode)
        {
            if (PluginLoader.Instance.TryGetPluginInstance(plugin.PluginData.Id, out PluginInstance instance))
                PluginInstance = instance;
        }

        InitializeInputContext();
    }

    public override void OnDispose()
    {
        base.OnDispose();

        OnScreenClosed?.Invoke();
    }
}
