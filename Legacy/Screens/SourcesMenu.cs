using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Pulsar.Legacy.Loader;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Sandbox;
using Sandbox.Graphics.GUI;
using VRage.Audio;
using VRageMath;

namespace Pulsar.Legacy.Screens
{
    internal class SourcesMenu(SourcesConfig sources) : PluginScreen(size: new Vector2(1, 0.9f))
    {
        private MyGuiControlParent hubsPanel;
        private MyGuiControlParent pluginsPanel;
        private MyGuiControlParent modsPanel;

        private readonly SourcesConfig Sources = sources;
        private readonly List<ModConfig> ModSources = [.. sources.ModSources];

        private readonly List<RemoteHubConfig> RemoteHubSources = [.. sources.RemoteHubSources];
        private readonly List<LocalHubConfig> LocalHubSources = [.. sources.LocalHubSources];

        private readonly List<RemotePluginConfig> RemotePluginSources =
        [
            .. sources.RemotePluginSources,
        ];
        private readonly List<LocalPluginConfig> LocalPluginSources =
        [
            .. sources.LocalPluginSources,
        ];

        private readonly Dictionary<object, bool> EnabledSourceChanges = [];

        private enum PluginType
        {
            Hub,
            Plugin,
            Mod,
        }

        public override string GetFriendlyName()
        {
            return typeof(AddPluginMenu).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            PluginList list = ConfigManager.Instance.List;

            // Top
            MyGuiControlLabel caption = AddCaption("Sources", captionScale: 1);
            AddBarBelow(caption);

            // Bottom
            Vector2 bottomMid = new(0, m_size.Value.Y / 2);
            MyGuiControlButton btnRefresh = new(
                position: new Vector2(bottomMid.X, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Refresh"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) =>
                {
                    // FIXME: Syncronise working copy and with real sources before refreshing
                    list.UpdateRemoteList(force: true);
                    list.UpdateLocalList();
                    RefreshSourcesLists();
                }
            );
            MyGuiControlButton btnApply = new(
                position: new Vector2(
                    bottomMid.X - 0.5f * btnRefresh.Size.X - GuiSpacing,
                    bottomMid.Y - GuiSpacing
                ),
                text: new StringBuilder("Apply"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) =>
                {
                    ApplyChanges();
                    CloseScreen();
                }
            );
            MyGuiControlButton btnCancel = new(
                position: new Vector2(
                    bottomMid.X + 0.5f * btnRefresh.Size.X + GuiSpacing,
                    bottomMid.Y - GuiSpacing
                ),
                text: new StringBuilder("Cancel"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => CloseScreen()
            );
            Controls.Add(btnApply);
            Controls.Add(btnRefresh);
            Controls.Add(btnCancel);
            AddBarAbove(btnApply);

            MyLayoutTable grid = GetLayoutTableBetween(
                caption,
                btnApply,
                verticalSpacing: GuiSpacing * 2
            );
            grid.SetColumnWidthsNormalized(0.3f, 0.3f, 0.3f);
            grid.SetRowHeightsNormalized(0.05f, 0.95f);

            // Column 1
            grid.Add(new MyGuiControlLabel(text: "Hubs"), MyAlignH.Center, MyAlignV.Bottom, 0, 0);
            hubsPanel = new MyGuiControlParent();
            grid.AddWithSize(hubsPanel, MyAlignH.Center, MyAlignV.Center, 1, 0);
            CreateSourcesPanel(hubsPanel, PluginType.Hub);

            // Column 2
            grid.Add(
                new MyGuiControlLabel(text: "Plugins"),
                MyAlignH.Center,
                MyAlignV.Bottom,
                0,
                1
            );
            pluginsPanel = new MyGuiControlParent();
            grid.AddWithSize(pluginsPanel, MyAlignH.Center, MyAlignV.Center, 1, 1);
            CreateSourcesPanel(pluginsPanel, PluginType.Plugin);

            // Column 2
            grid.Add(new MyGuiControlLabel(text: "Mods"), MyAlignH.Center, MyAlignV.Bottom, 0, 2);
            modsPanel = new MyGuiControlParent();
            grid.AddWithSize(modsPanel, MyAlignH.Center, MyAlignV.Center, 1, 2);
            CreateSourcesPanel(modsPanel, PluginType.Mod);
        }

        private void OpenSourceView(object source)
        {
            if (source is null)
                return;

            ViewSourceMenu view = new(source, RemoveSource);
            MyGuiSandbox.AddScreen(view);
        }

        private void CreateSourcesPanel(MyGuiControlParent parent, PluginType type)
        {
            const float ButtonSpacing = 0.5f * GuiSpacing;

            Vector2 topLeft = parent.Size * -0.5f;
            MyGuiControlButton template = new(
                visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall
            );
            MyGuiControlTable list = CreateSourceTable(parent.Size, template.Size.Y, type);
            list.ItemDoubleClicked += (x, _) => OpenSourceView(x.SelectedRow.UserData);
            parent.Controls.Add(list);

            if (type == PluginType.Hub)
            {
                MyGuiControlButton remoteHubAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Remote Hub",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: (x) => MyGuiSandbox.AddScreen(new AddRemoteHub(AddRemoteHub))
                )
                {
                    Position = new Vector2(
                        -topLeft.X - template.Size.X - ButtonSpacing,
                        topLeft.Y + list.Size.Y + ButtonSpacing
                    ),
                };
                AddImageToButton(remoteHubAdd, @"Textures\GUI\link.dds");
                parent.Controls.Add(remoteHubAdd);

                MyGuiControlButton localHubAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Local Hub",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: AddLocalHub
                )
                {
                    Position = new Vector2(-topLeft.X, topLeft.Y + list.Size.Y + ButtonSpacing),
                };
                AddImageToButton(localHubAdd, @"Textures\GUI\Controls\button_increase.dds");
                parent.Controls.Add(localHubAdd);
            }
            else if (type == PluginType.Plugin)
            {
                MyGuiControlButton remotePluginAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Remote Plugin",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: (x) =>
                        MyGuiSandbox.AddScreen(new AddRemotePlugin(AddRemotePlugin))
                )
                {
                    Position = new Vector2(
                        -topLeft.X - 2 * template.Size.X - 2 * ButtonSpacing,
                        topLeft.Y + list.Size.Y + ButtonSpacing
                    ),
                };
                AddImageToButton(remotePluginAdd, @"Textures\GUI\link.dds");
                parent.Controls.Add(remotePluginAdd);

                MyGuiControlButton folderPluginAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Development Folder",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: AddDevelopmentFolder
                )
                {
                    Position = new Vector2(
                        -topLeft.X - template.Size.X - ButtonSpacing,
                        topLeft.Y + list.Size.Y + ButtonSpacing
                    ),
                };
                AddImageToButton(folderPluginAdd, @"Textures\GUI\Controls\button_increase.dds");
                parent.Controls.Add(folderPluginAdd);

                MyGuiControlButton copmiledPluginAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Precompiled Plugin",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: AddLocalFile
                )
                {
                    Position = new Vector2(-topLeft.X, topLeft.Y + list.Size.Y + ButtonSpacing),
                };
                AddImageToButton(copmiledPluginAdd, @"Textures\GUI\Controls\button_increase.dds");
                parent.Controls.Add(copmiledPluginAdd);
            }
            else if (type == PluginType.Mod)
            {
                MyGuiControlButton modAdd = new(
                    visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                    toolTip: "Add Mod",
                    originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: (x) => MyGuiSandbox.AddScreen(new AddMod(AddMod))
                )
                {
                    Position = new Vector2(-topLeft.X, topLeft.Y + list.Size.Y + ButtonSpacing),
                };
                AddImageToButton(modAdd, @"Textures\GUI\link.dds");
                parent.Controls.Add(modAdd);
            }
        }

        private void ApplyChanges()
        {
            PluginList list = ConfigManager.Instance.List;

            Sources.RemoteHubSources = [.. RemoteHubSources];
            Sources.LocalHubSources = [.. LocalHubSources];
            Sources.RemotePluginSources = [.. RemotePluginSources];
            Sources.LocalPluginSources = [.. LocalPluginSources];
            Sources.ModSources = [.. ModSources];

            ApplyEnabledChanges();

            list.UpdateRemoteList();
            list.UpdateLocalList();
            Sources.Save();
        }

        private void RemoveSource(object source)
        {
            if (source is RemoteHubConfig remoteHub)
                RemoteHubSources.Remove(remoteHub);
            else if (source is LocalHubConfig localHub)
                LocalHubSources.Remove(localHub);
            else if (source is RemotePluginConfig remotePlugin)
                RemotePluginSources.Remove(remotePlugin);
            else if (source is LocalPluginConfig localPlugin)
                LocalPluginSources.Remove(localPlugin);
            else if (source is ModConfig mod)
                ModSources.Remove(mod);

            RefreshSourcesLists();
        }

        private void ApplyEnabledChanges()
        {
            foreach (KeyValuePair<object, bool> kvp in EnabledSourceChanges)
            {
                object source = kvp.Key;
                if (source is RemoteHubConfig remoteHub)
                    remoteHub.Enabled = kvp.Value;
                else if (source is LocalHubConfig localHub)
                    localHub.Enabled = kvp.Value;
                else if (source is RemotePluginConfig remotePlugin)
                    remotePlugin.Enabled = kvp.Value;
                else if (source is LocalPluginConfig localPlugin)
                    localPlugin.Enabled = kvp.Value;
                else if (source is ModConfig mod)
                    mod.Enabled = kvp.Value;
            }
        }

        private MyGuiControlTable CreateSourceTable(
            Vector2 parentSize,
            float bottomPadding,
            PluginType type
        )
        {
            MyGuiControlTable list = new()
            {
                Position = parentSize * -0.5f, // Top left
                Size = new Vector2(parentSize.X, 0), // VisibleRowsCount controls y size
                OriginAlign = VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
            };

            SetTableHeight(list, parentSize.Y - bottomPadding);

            if (type == PluginType.Mod)
            {
                list.ColumnsCount = 2;
                list.SetCustomColumnWidths([0.9f, 0.1f]);
                list.SetColumnName(0, new StringBuilder("Name"));
            }
            else
            {
                list.ColumnsCount = 3;
                list.SetCustomColumnWidths([0.45f, 0.45f, 0.1f]);
                list.SetColumnName(0, new StringBuilder("Name"));
                list.SetColumnName(1, new StringBuilder("Update"));
            }

            list.SetColumnComparison(0, CellTextComparison);
            list.SortByColumn(0, MyGuiControlTable.SortStateEnum.Ascending, true);
            PopulateList(list, type);
            return list;
        }

        private void RefreshSourcesLists()
        {
            hubsPanel.Controls.Clear();
            CreateSourcesPanel(hubsPanel, PluginType.Hub);
            pluginsPanel.Controls.Clear();
            CreateSourcesPanel(pluginsPanel, PluginType.Plugin);
            modsPanel.Controls.Clear();
            CreateSourcesPanel(modsPanel, PluginType.Mod);
        }

        private int CellTextComparison(MyGuiControlTable.Cell x, MyGuiControlTable.Cell y)
        {
            return TextComparison(x.Text, y.Text);
        }

        private static int TextComparison(StringBuilder x, StringBuilder y)
        {
            if (x == null)
            {
                if (y == null)
                    return 0;
                return 1;
            }

            if (y == null)
                return -1;

            return x.CompareTo(y);
        }

        private void MakeHubRows(MyGuiControlTable list)
        {
            foreach (RemoteHubConfig source in RemoteHubSources)
            {
                var row = new MyGuiControlTable.Row(source);

                MyGuiHighlightTexture? icon = null;
                if (source.Trusted)
                    icon = new MyGuiHighlightTexture
                    {
                        Normal = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Highlight = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Focus = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Active = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        SizePx = new Vector2(40f, 40f),
                    };

                var nameCell = new MyGuiControlTable.Cell(
                    source.Name,
                    iconOriginAlign: VRage
                        .Utils
                        .MyGuiDrawAlignEnum
                        .HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                    icon: icon
                );
                row.AddCell(nameCell);

                row.AddCell(new MyGuiControlTable.Cell(Tools.DateToString(source.LastCheck)));

                var enabledCell = new MyGuiControlTable.Cell(source.Enabled.ToString());
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: source.Enabled)
                {
                    UserData = source,
                    Visible = true,
                };
                enabledCheckbox.IsCheckedChanged += (x) =>
                    EnabledSourceChanges[source] = enabledCheckbox.IsChecked;
                enabledCell.Control = enabledCheckbox;
                list.Controls.Add(enabledCheckbox);
                row.AddCell(enabledCell);

                list.Add(row);
            }

            foreach (LocalHubConfig source in LocalHubSources)
            {
                var row = new MyGuiControlTable.Row(source);
                row.AddCell(new MyGuiControlTable.Cell(source.Name));
                row.AddCell(new MyGuiControlTable.Cell("-"));

                var enabledCell = new MyGuiControlTable.Cell(source.Enabled.ToString());
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: source.Enabled)
                {
                    UserData = source,
                    Visible = true,
                };
                enabledCheckbox.IsCheckedChanged += (x) =>
                    EnabledSourceChanges[source] = enabledCheckbox.IsChecked;
                enabledCell.Control = enabledCheckbox;
                list.Controls.Add(enabledCheckbox);
                row.AddCell(enabledCell);

                list.Add(row);
            }
        }

        private void MakePluginRows(MyGuiControlTable list)
        {
            foreach (RemotePluginConfig source in RemotePluginSources)
            {
                var row = new MyGuiControlTable.Row(source);

                MyGuiHighlightTexture? icon = null;
                if (source.Trusted)
                    icon = new MyGuiHighlightTexture
                    {
                        Normal = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Highlight = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Focus = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        Active = @"Textures\GUI\Icons\buttons\ModModioIcon.dds",
                        SizePx = new Vector2(40f, 40f),
                    };

                var nameCell = new MyGuiControlTable.Cell(
                    source.Name,
                    iconOriginAlign: VRage
                        .Utils
                        .MyGuiDrawAlignEnum
                        .HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                    icon: icon
                );
                row.AddCell(nameCell);

                row.AddCell(new MyGuiControlTable.Cell(Tools.DateToString(source.LastCheck)));

                var enabledCell = new MyGuiControlTable.Cell(source.Enabled.ToString());
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: source.Enabled)
                {
                    UserData = source,
                    Visible = true,
                };
                enabledCheckbox.IsCheckedChanged += (x) =>
                    EnabledSourceChanges[source] = enabledCheckbox.IsChecked;
                enabledCell.Control = enabledCheckbox;
                list.Controls.Add(enabledCheckbox);
                row.AddCell(enabledCell);

                list.Add(row);
            }

            foreach (LocalPluginConfig source in LocalPluginSources)
            {
                var row = new MyGuiControlTable.Row(source);
                row.AddCell(new MyGuiControlTable.Cell(source.Name));
                row.AddCell(new MyGuiControlTable.Cell("-"));

                var enabledCell = new MyGuiControlTable.Cell(source.Enabled.ToString());
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: source.Enabled)
                {
                    UserData = source,
                    Visible = true,
                };
                enabledCheckbox.IsCheckedChanged += (x) =>
                    source.Enabled = enabledCheckbox.IsChecked;
                enabledCell.Control = enabledCheckbox;
                list.Controls.Add(enabledCheckbox);
                row.AddCell(enabledCell);

                list.Add(row);
            }
        }

        private void MakeModRows(MyGuiControlTable list)
        {
            foreach (ModConfig source in ModSources)
            {
                var row = new MyGuiControlTable.Row(source);
                row.AddCell(new MyGuiControlTable.Cell(source.Name));

                var enabledCell = new MyGuiControlTable.Cell(source.Enabled.ToString());
                var enabledCheckbox = new MyGuiControlCheckbox(isChecked: source.Enabled)
                {
                    UserData = source,
                    Visible = true,
                };
                enabledCheckbox.IsCheckedChanged += (x) =>
                    source.Enabled = enabledCheckbox.IsChecked;
                enabledCell.Control = enabledCheckbox;
                list.Controls.Add(enabledCheckbox);
                row.AddCell(enabledCell);

                list.Add(row);
            }
        }

        private void PopulateList(MyGuiControlTable list, PluginType type)
        {
            list.Clear();
            list.Controls.Clear();

            if (type == PluginType.Hub)
                MakeHubRows(list);
            else if (type == PluginType.Plugin)
                MakePluginRows(list);
            else if (type == PluginType.Mod)
                MakeModRows(list);

            if (list.RowsCount == 0)
            {
                var row = new MyGuiControlTable.Row();
                string helpText = "Add sources with the buttons below";
                row.AddCell(new MyGuiControlTable.Cell(text: helpText, toolTip: helpText));
                list.Add(row);
                for (int i = 1; i < list.ColumnsCount; i++)
                    list.SetColumnVisibility(i, false);
            }
            else
            {
                for (int i = 1; i < list.ColumnsCount; i++)
                    list.SetColumnVisibility(i, true);
            }
        }

        private void AddLocalHub(MyGuiControlButton btn)
        {
            MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
            Tools.OpenFolderDialog(
                "Open the root of your project",
                (folder) =>
                {
                    bool exists = LocalHubSources.Any(p =>
                        string.Equals(p.Folder, folder, StringComparison.OrdinalIgnoreCase)
                    );
                    if (exists)
                    {
                        MyGuiSandbox.AddScreen(
                            MyGuiSandbox.CreateMessageBox(
                                MyMessageBoxStyleEnum.Error,
                                messageText: new StringBuilder("That local hub already exists!"),
                                messageCaption: new StringBuilder("Pulsar")
                            )
                        );
                        return;
                    }

                    LocalHubConfig hub = new()
                    {
                        Name = new DirectoryInfo(folder).Name,
                        Folder = folder,
                        Enabled = true,
                    };
                    LocalHubSources.Add(hub);
                    RefreshSourcesLists();
                }
            );
        }

        private void AddDevelopmentFolder(MyGuiControlButton btn)
        {
            MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
            PromptFolder(
                (folder) =>
                {
                    LocalPluginConfig plugin = new()
                    {
                        Name = Path.GetFileName(folder),
                        Folder = folder,
                        Enabled = true,
                    };
                    LocalPluginSources.Add(plugin);
                    RefreshSourcesLists();
                }
            );
        }

        public static void PromptFolder(Action<string> onComplete)
        {
            Tools.OpenFolderDialog(
                "Open the root of your project",
                (folder) =>
                {
                    if (ConfigManager.Instance.List.Contains(folder))
                    {
                        MyGuiSandbox.AddScreen(
                            MyGuiSandbox.CreateMessageBox(
                                MyMessageBoxStyleEnum.Error,
                                messageText: new StringBuilder(
                                    "That development folder already exists!"
                                ),
                                messageCaption: new StringBuilder("Pulsar")
                            )
                        );
                        return;
                    }

                    onComplete(folder);
                }
            );
        }

        private void AddLocalFile(MyGuiControlButton btn)
        {
            try
            {
                string localPlugins = Path.Combine(LoaderTools.PulsarDir, "Local");
                Directory.CreateDirectory(localPlugins);
                Process.Start("explorer.exe", $"\"{localPlugins}\"");
                MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening local plugins folder: " + e);
            }
        }

        private void AddRemoteHub(RemoteHubConfig source)
        {
            if (RemoteHubSources.Any((x) => x.Repo == source.Repo))
            {
                MyGuiSandbox.CreateMessageBox(
                    messageCaption: new StringBuilder("Source Error"),
                    messageText: new StringBuilder("This source already exists in the list.")
                );
            }
            else
            {
                RemoteHubSources.Add(source);
                RefreshSourcesLists();
            }
        }

        private void AddRemotePlugin(RemotePluginConfig source)
        {
            if (RemotePluginSources.Any((x) => x.Repo == source.Repo))
            {
                MyGuiSandbox.CreateMessageBox(
                    messageCaption: new StringBuilder("Source Error"),
                    messageText: new StringBuilder("This source already exists in the list.")
                );
            }
            else
            {
                RemotePluginSources.Add(source);
                RefreshSourcesLists();
            }
        }

        private void AddMod(ModConfig source)
        {
            if (ModSources.Any((x) => x.ID == source.ID))
            {
                MyGuiSandbox.CreateMessageBox(
                    messageCaption: new StringBuilder("Source Error"),
                    messageText: new StringBuilder("This source already exists in the list.")
                );
            }
            else
            {
                ModSources.Add(source);
                RefreshSourcesLists();
            }
        }
    }
}
