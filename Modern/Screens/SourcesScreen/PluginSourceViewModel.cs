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

            if (config is RemotePluginConfig remotePlugin)
                return remotePlugin.Name;

            if (config is LocalPluginConfig localPlugin)
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

            if (config is RemotePluginConfig remotePlugin)
                return Tools.DateToString(remotePlugin.LastCheck);

            if (config is LocalPluginConfig)
                return "-";

            return null;
        }
    }


    private bool isDummy = false;
    private object config;

    public PluginSourceViewModel(RemotePluginConfig config) 
    { 
        this.config = config;
    }

    public PluginSourceViewModel(LocalPluginConfig config)
    {
        this.config = config;
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
