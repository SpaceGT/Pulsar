using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen;

internal class ModSourceViewModel : AttachedViewModel
{
    public string Name
    {
        get
        {
            if (isDummy)
                return "Dummy Mod";

            return config.Name;
        }
    }

    public long Id => config.ID;

    public bool IsEnabled => config.Enabled;

    private readonly bool isDummy = false;
    private readonly ModConfig config;

    public ModSourceViewModel(ModConfig config)
    {
        this.config = config;
    }

    private ModSourceViewModel()
    {
        isDummy = true;
    }

    public static ModSourceViewModel GetDummyViewModel()
    {
        return new ModSourceViewModel();
    }
}
