using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class PluginSourceViewModel : AttachedViewModel
{
    public string Name
    {
        get
        {
            if (isDummy)
                return "Dummy Plugin";

            if (Config is RemotePluginConfig remotePlugin)
                return remotePlugin.Name;

            if (Config is LocalPluginConfig localPlugin)
                return localPlugin.Name;

            return null;
        }
    }

    public string LastCheckString
    {
        get
        {
            if (isDummy)
                return "59 minutes ago";

            if (Config is RemotePluginConfig remotePlugin)
                return Tools.DateToString(remotePlugin.LastCheck);

            if (Config is LocalPluginConfig)
                return "-";

            return null;
        }
    }

    public bool IsTrusted
    {
        get
        {
            if (Config is RemotePluginConfig remoteHub)
                return remoteHub.Trusted;

            return false;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (Config is RemotePluginConfig remoteHub)
                return remoteHub.Enabled;

            if (Config is LocalPluginConfig localHub)
                return localHub.Enabled;

            return false;
        }
    }

    public readonly object Config;

    private readonly bool isDummy = false;
    

    public PluginSourceViewModel(RemotePluginConfig config)
    {
        this.Config = config;
    }

    public PluginSourceViewModel(LocalPluginConfig config)
    {
        this.Config = config;
    }

    private PluginSourceViewModel()
    {
        isDummy = true;
    }

    public static PluginSourceViewModel GetDummyViewModel()
    {
        return new PluginSourceViewModel();
    }
}
