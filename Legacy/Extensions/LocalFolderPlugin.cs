using System.Diagnostics;
using System.IO;
using System.Text;
using Pulsar.Legacy.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Extensions
{
    internal static class LocalFolderPluginExtensions
    {
        public static void AddDetailControls(
            this LocalFolderPlugin localFolderPlugin,
            PluginDetailMenu screen,
            MyGuiControlBase bottomControl,
            out MyGuiControlBase topControl
        )
        {
            MyGuiControlButton btnRemove = new(
                text: new StringBuilder("Remove"),
                onButtonClick: (btn) =>
                {
                    PluginConfig config = ConfigManager.Instance.Config;
                    config.Save();
                    screen.CloseScreen();
                    screen.InvokeOnPluginRemoved(localFolderPlugin);
                    screen.InvokeOnRestartRequired();
                }
            );
            screen.PositionAbove(bottomControl, btnRemove);
            screen.Controls.Add(btnRemove);

            MyGuiControlButton btnLoadFile = new(
                text: new StringBuilder("Load File"),
                onButtonClick: (btn) =>
                {
                    localFolderPlugin.LoadNewDataFile(() =>
                    {
                        screen.CloseScreen();
                    });
                }
            );
            screen.PositionToRight(btnRemove, btnLoadFile);
            LocalFolderConfig folderSettings = localFolderPlugin.FolderSettings;
            btnLoadFile.Enabled =
                string.IsNullOrEmpty(folderSettings.DataFile)
                || !File.Exists(folderSettings.DataFile);
            screen.Controls.Add(btnLoadFile);

            MyGuiControlCombobox releaseDropdown = new();
            releaseDropdown.AddItem(0, "Release");
            releaseDropdown.AddItem(1, "Debug");
            releaseDropdown.SelectItemByKey(folderSettings.DebugBuild ? 1 : 0);
            releaseDropdown.ItemSelected += () =>
            {
                folderSettings.DebugBuild = releaseDropdown.GetSelectedKey() == 1;
                ConfigManager.Instance.Config.Save();
                screen.InvokeOnRestartRequired();
            };
            screen.PositionAbove(btnRemove, releaseDropdown, MyAlignH.Left);
            screen.Controls.Add(releaseDropdown);
            topControl = releaseDropdown;
        }

        public static void Show(this LocalFolderPlugin localFolderPlugin)
        {
            string folder = Path.GetFullPath(localFolderPlugin.Id);
            if (Directory.Exists(folder))
                Process.Start("explorer.exe", $"\"{folder}\"");
        }
    }
}
