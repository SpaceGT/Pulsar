using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
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

        public static ProfileViewModel GetDummyProfileViewModel()
        {
            Profile dummyProfile = new Profile("Dummy Profile");
            dummyProfile.GitHub.Add(new GitHubPluginConfig());
            dummyProfile.Local.Add(string.Empty);
            dummyProfile.Mods.Add(0);

            return new(dummyProfile);
        }
    }
}
