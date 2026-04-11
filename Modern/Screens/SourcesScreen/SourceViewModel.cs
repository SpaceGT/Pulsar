using System;
using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class SourceViewModel(object config) : AttachedViewModel
{
    public string Name
    {
        get =>
            config switch
            {
                RemoteHubConfig remoteHub => remoteHub.Name,
                LocalHubConfig localHub => localHub.Name,
                RemotePluginConfig remotePlugin => remotePlugin.Name,
                LocalPluginConfig localPlugin => localPlugin.Name,
                ModConfig modConfig => modConfig.Name,
                _ => null,
            };
    }

    public string LastCheckedText
    {
        get =>
            config switch
            {
                RemoteHubConfig remoteHub => Tools.DateToString(remoteHub.LastCheck),
                RemotePluginConfig remotePlugin => Tools.DateToString(remotePlugin.LastCheck),
                _ => "-",
            };
    }

    public bool IsTrusted
    {
        get =>
            config switch
            {
                RemoteHubConfig remoteHub => remoteHub.Trusted,
                RemotePluginConfig remotePlugin => remotePlugin.Trusted,
                _ => false,
            };
    }

    public bool IsEnabled
    {
        get =>
            config switch
            {
                RemoteHubConfig remoteHub => remoteHub.Enabled,
                LocalHubConfig localHub => localHub.Enabled,
                RemotePluginConfig remotePlugin => remotePlugin.Enabled,
                LocalPluginConfig localPlugin => localPlugin.Enabled,
                ModConfig modConfig => modConfig.Enabled,
                _ => false,
            };
    }

    public string Hash
    {
        get =>
            config switch
            {
                RemoteHubConfig remoteHub => remoteHub.Hash,
                LocalHubConfig localHub => localHub.Hash,
                _ => "NOT_A_HUB",
            };
    }

    public long WorkshopId
    {
        get =>
            config switch
            {
                ModConfig modConfig => modConfig.ID,
                _ => 0,
            };
    }

    public bool IsHub => Hash != "NOT_A_HUB";

    public bool IsPlugin => !IsHub && WorkshopId == 0;

    public object Config => config;

    public static SourceViewModel GetDummyHubViewModel()
    {
        RemoteHubConfig dummyHub = new()
        {
            Name = "Dummy Hub",
            LastCheck = DateTime.UtcNow - new TimeSpan(0, 59, 0),
            Trusted = true,
        };

        return new(dummyHub);
    }

    public static SourceViewModel GetDummyPluginViewModel()
    {
        RemotePluginConfig dummyPlugin = new()
        {
            Name = "Dummy Plugin",
            LastCheck = DateTime.UtcNow - new TimeSpan(0, 59, 0),
            Trusted = true,
        };

        return new(dummyPlugin);
    }

    public static SourceViewModel GetDummyModViewModel()
    {
        ModConfig modPlugin = new() { Name = "Dummy Mod" };

        return new(modPlugin);
    }
}
