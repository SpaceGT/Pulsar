using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class HubSourceViewModel : AttachedViewModel
{
    public string Name
    {
        get
        {
            if (isDummy)
                return "Dummy Hub";

            if (config is RemoteHubConfig remoteHub)
                return remoteHub.Name;

            if (config is LocalHubConfig localHub)
                return localHub.Name;

            return null;
        }
    }

    public string LastCheckString
    {
        get
        {
            if (isDummy)
                return "59 minutes ago";

            if (config is RemoteHubConfig remoteHub)
                return Tools.DateToString(remoteHub.LastCheck);

            if (config is LocalHubConfig)
                return "-";

            return null;
        }
    }


    private bool isDummy = false;
    private object config;

    public HubSourceViewModel(RemoteHubConfig config)
    {
        this.config = config;
    }

    public HubSourceViewModel(LocalHubConfig config)
    {
        this.config = config;
    }

    private HubSourceViewModel()
    {
        isDummy = true;
    }

    public static HubSourceViewModel GetDummyViewModel()
    {
        return new HubSourceViewModel();
    }
}
