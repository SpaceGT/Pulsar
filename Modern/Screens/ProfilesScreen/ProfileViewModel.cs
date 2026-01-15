using Keen.VRage.UI.Screens;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.ProfilesScreen
{
    internal class ProfileViewModel : AttachedViewModel
    {
        public string Name => profile.Name;
        public string Description => profile.GetDescription();

        private Profile profile;

        public ProfileViewModel(Profile profile)
        {
            this.profile = profile;
        }
    }
}
