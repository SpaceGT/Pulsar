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
        private static void TrySaveSettings(string id)
        {
            ProfilesConfig profiles = ConfigManager.Instance.Profiles;
            if (profiles.Current.Contains(id))
            {
                profiles.Current.Update(id);
                profiles.Save();
            }
        }

        public static void AddDetailControls(
            this LocalFolderPlugin localFolderPlugin,
            PluginDetailMenu screen,
            MyGuiControlBase bottomControl,
            out MyGuiControlBase topControl
        )
        {
            LocalFolderConfig folderSettings = localFolderPlugin.FolderSettings;
            MyGuiControlButton btnRemove = new(
                text: new StringBuilder("Remove File"),
                onButtonClick: (btn) =>
                {
                    localFolderPlugin.DeserializeFile(null);
                    TrySaveSettings(folderSettings.Id);
                    screen.RecreateControls(false);
                }
            );

            if (folderSettings.DataFile == null)
                btnRemove.Enabled = false;

            screen.PositionAbove(bottomControl, btnRemove);
            screen.Controls.Add(btnRemove);

            MyGuiControlButton btnLoadFile = new(
                text: new StringBuilder("Load File"),
                onButtonClick: (btn) =>
                    localFolderPlugin.LoadNewDataFile(() =>
                    {
                        TrySaveSettings(folderSettings.Id);
                        btnRemove.Enabled = true;
                        screen.RecreateControls(false);
                    })
            );
            screen.PositionToRight(btnRemove, btnLoadFile);
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
                TrySaveSettings(folderSettings.Id);
                screen.InvokeOnRestartRequired();
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
}
