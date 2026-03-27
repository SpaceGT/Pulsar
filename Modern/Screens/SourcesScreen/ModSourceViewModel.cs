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

            return Config.Name;
        }
    }

    public long Id => Config.ID;

    public bool IsEnabled => Config.Enabled;

    public readonly ModConfig Config;

    private readonly bool isDummy = false;
    
    public ModSourceViewModel(ModConfig config)
    {
        this.Config = config;
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
