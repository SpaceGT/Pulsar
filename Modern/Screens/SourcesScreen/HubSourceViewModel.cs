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

            if (Config is RemoteHubConfig remoteHub)
                return remoteHub.Name;

            if (Config is LocalHubConfig localHub)
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

            if (Config is RemoteHubConfig remoteHub)
                return Tools.DateToString(remoteHub.LastCheck);

            if (Config is LocalHubConfig)
                return "-";

            return null;
        }
    }

    public bool IsTrusted
    {
        get
        {
            if (Config is RemoteHubConfig remoteHub)
                return remoteHub.Trusted;

            return false;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (Config is RemoteHubConfig remoteHub)
                return remoteHub.Enabled;

            if (Config is LocalHubConfig localHub)
                return localHub.Enabled;

            return false;
        }
    }

    public string Hash
    {
        get 
        {
            if (Config is RemoteHubConfig remoteHub)
                return remoteHub.Hash;

            if (Config is LocalHubConfig localHub)
                return localHub.Hash;

            return null;
        }
    }

    public readonly object Config;

    private readonly bool isDummy = false;
    
    public HubSourceViewModel(RemoteHubConfig config)
    {
        this.Config = config;
    }

    public HubSourceViewModel(LocalHubConfig config)
    {
        this.Config = config;
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
