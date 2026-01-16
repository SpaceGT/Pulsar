using Keen.VRage.UI.Screens;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.ProfilesScreen
{
    internal class ProfileViewModel : AttachedViewModel
    {
        public readonly Profile Profile;

        public string Name => Profile.Name;
        public string Description => Profile.GetDescription();

        public ProfileViewModel(Profile profile)
        {
            Profile = profile;
        }
    }
}
