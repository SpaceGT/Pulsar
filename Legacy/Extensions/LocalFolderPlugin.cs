using System.Diagnostics;
using System.IO;
using System.Text;
using Pulsar.Legacy.Screens;
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

        MyGuiControlButton btnRemove = new(
            text: new StringBuilder("Remove File"),
            onButtonClick: (btn) =>
            {
                localFolderPlugin.DeserializeFile(null);
                draftConfig.DataFile = null;
                screen.RecreateControls(false);
            }
        )
        {
            Enabled = draftConfig?.DataFile is not null,
        };

        screen.PositionAbove(bottomControl, btnRemove);
        screen.Controls.Add(btnRemove);

        MyGuiControlButton btnLoadFile = new(
            text: new StringBuilder("Load File"),
            onButtonClick: (btn) =>
                localFolderPlugin.LoadNewDataFile(
                    (file) =>
                    {
                        draftConfig.DataFile = file;
                        btnRemove.Enabled = true;
                        screen.RecreateControls(false);
                    }
                )
        );
        screen.PositionToRight(btnRemove, btnLoadFile);
        btnLoadFile.Enabled =
            draftConfig is not null
            && (string.IsNullOrEmpty(draftConfig.DataFile) || !File.Exists(draftConfig.DataFile));
        screen.Controls.Add(btnLoadFile);

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
        screen.PositionAbove(btnRemove, releaseDropdown, MyAlignH.Left);
        screen.Controls.Add(releaseDropdown);
        topControl = releaseDropdown;
    }

    public static void Show(this LocalFolderPlugin localFolderPlugin)
    {
        string folder = Path.GetFullPath(localFolderPlugin.Folder);
        if (Directory.Exists(folder))
            Process.Start("explorer.exe", $"\"{folder}\"");
    }
}
