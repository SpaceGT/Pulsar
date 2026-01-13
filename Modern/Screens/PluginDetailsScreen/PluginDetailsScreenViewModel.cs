using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Modern.Loader;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pulsar.Modern.Screens.PluginDetailsScreen;

internal class PluginDetailsScreenViewModel : ScreenViewModel
{
    public PluginViewModel Plugin { get; private set; }
    public readonly PluginInstance PluginInstance;
    public readonly Profile Draft;

    public event Action OnScreenClose;

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

        OnScreenClose?.Invoke();
    }
}
