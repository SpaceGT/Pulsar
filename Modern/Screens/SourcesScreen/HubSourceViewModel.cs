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

    public bool IsTrusted
    {
        get
        {
            if (config is RemoteHubConfig remoteHub)
                return remoteHub.Trusted;

            return false;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (config is RemoteHubConfig remoteHub)
                return remoteHub.Enabled;

            if (config is LocalHubConfig localHub)
                return localHub.Enabled;

            return false;
        }
    }

    private readonly bool isDummy = false;
    private readonly object config;

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
