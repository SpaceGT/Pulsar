using System;
using System.Collections.Generic;
using Keen.VRage.UI.Screens;
using Pulsar.Modern.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.PluginConfigurationScreen;

internal class PluginConfigurationScreenViewModel : ScreenViewModel
{
    public List<PluginViewModel> Plugins { get; private set; } = [];

    public bool NoPlugins => Plugins.Count < 1;

    public PluginConfigurationScreenViewModel(PluginList list)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        InitializeInputContext();

        foreach (PluginData plugin in list)
        {
            if (plugin is not ModPlugin modPlugin)
            {
                if (
                    !PluginLoader.Instance.TryGetPluginInstance(
                        plugin.Id,
                        out PluginInstance instance
                    )
                )
                    continue;

                if (instance.HasConfigDialog)
                    Plugins.Add(new PluginViewModel(plugin, null));
            }
        }

        Plugins.Sort(ComparePluginsByName);
    }

    private PluginConfigurationScreenViewModel(List<PluginViewModel> list)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        InitializeInputContext();

        Plugins = list;
    }

    public static PluginConfigurationScreenViewModel GetDummyVm()
    {
        List<PluginViewModel> dummyPlugins = [];

        for (int i = 0; i < 25; i++)
        {
            dummyPlugins.Add(PluginViewModel.GetDummyPlugin());
        }

        return new PluginConfigurationScreenViewModel(dummyPlugins);
    }

    private int ComparePluginsByName(PluginViewModel x, PluginViewModel y)
    {
        return x.FriendlyName.CompareTo(y.FriendlyName, StringComparison.OrdinalIgnoreCase);
    }
}
