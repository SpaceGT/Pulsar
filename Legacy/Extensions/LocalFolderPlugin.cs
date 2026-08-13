using Pulsar.Legacy.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Extensions;

internal static class LocalFolderPluginExtensions
{
    public static void AddDetailControls(
        this LocalFolderPlugin localFolderPlugin,
        PluginDetailMenu screen,
        MyGuiControlBase bottomControl,
        out MyGuiControlBase topControl
    )
    {
        var draftConfig = (LocalFolderConfig)screen.draft.GetData(localFolderPlugin.Id);

        MyGuiControlCombobox releaseDropdown = new();
        releaseDropdown.AddItem(0, "Release");
        releaseDropdown.AddItem(1, "Debug");
        releaseDropdown.SelectItemByKey((draftConfig ?? new()).DebugBuild ? 1 : 0);
        releaseDropdown.Enabled = draftConfig is not null;
        releaseDropdown.ItemSelected += () =>
        {
            bool isDebug = releaseDropdown.GetSelectedKey() == 1;
            draftConfig.DebugBuild = isDebug;
        };
        screen.PositionAbove(bottomControl, releaseDropdown);
        releaseDropdown.Position = new(0, releaseDropdown.Position.Y);

        screen.Controls.Add(releaseDropdown);
        topControl = releaseDropdown;
    }

    public static void Show(this LocalFolderPlugin localFolderPlugin)
    {
        Tools.ShowInFileManager(localFolderPlugin.Folder);
    }
}
