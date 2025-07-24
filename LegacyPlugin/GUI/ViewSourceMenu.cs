using System;
using System.Text;
using Pulsar.Shared.Config;
using Sandbox.Graphics.GUI;
using VRageMath;
using static Sandbox.Graphics.GUI.MyGuiControlTable;

namespace Pulsar.Legacy.Plugin.GUI
{
    internal class ViewSourceMenu(object source, Action<object> onRemove)
        : PluginScreen(size: new Vector2(0.75f, 0.5f))
    {
        private readonly object Source = source;
        private readonly Action<object> OnRemove = onRemove;
        private MyGuiControlButton CloseButton;
        private MyGuiControlButton RemoveButton;

        public override string GetFriendlyName()
        {
            return typeof(ViewSourceMenu).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            MyGuiControlLabel caption = AddCaption("Source Info", captionScale: 1.2f);
            AddBarBelow(caption);

            Vector2 bottomMid = new(0, m_size.Value.Y / 2);

            CloseButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X - GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Back"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => CloseScreen()
            );
            RemoveButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X + GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Remove"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => PromptRemoveSource()
            );

            Controls.Add(RemoveButton);
            Controls.Add(CloseButton);

            MyGuiControlTable list = CreateSourceTable(
                size: new Vector2(
                    m_size.Value.X - GuiSpacing * 2,
                    m_size.Value.Y - (caption.Size.Y + CloseButton.Size.Y + GuiSpacing)
                ),
                position: new Vector2(
                    0,
                    0.5f * m_size.Value.Y - (CloseButton.Size.Y + GuiSpacing * 2)
                ),
                bottomPadding: CloseButton.Size.Y + GuiSpacing * 2
            );

            Controls.Add(list);
        }

        private MyGuiControlTable CreateSourceTable(
            Vector2 size,
            Vector2 position,
            float bottomPadding
        )
        {
            MyGuiControlTable list = new()
            {
                Position = position,
                Size = new Vector2(size.X, 0), // VisibleRowsCount controls y size
                OriginAlign = VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
            };

            SetTableHeight(list, size.Y - bottomPadding);

            list.ColumnsCount = 2;
            list.HeaderVisible = false;
            list.BorderHighlightEnabled = false;
            list.SetCustomColumnWidths([0.2f, 0.8f]);
            list.SetColumnComparison(0, CellTextComparison);
            list.SortByColumn(0, SortStateEnum.Ascending, true);

            PopulateList(list);
            return list;
        }

        private int CellTextComparison(Cell x, Cell y)
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

        private void RemoteHubList(MyGuiControlTable list, RemoteHubConfig remoteHub)
        {
            {
                var row = new Row();
                row.AddCell(new Cell("Name"));
                row.AddCell(new Cell(remoteHub.Name));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Repo"));
                row.AddCell(new Cell(remoteHub.Repo));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Branch"));
                row.AddCell(new Cell(remoteHub.Branch));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Last Check"));
                row.AddCell(new Cell(DateToString(remoteHub.LastCheck)));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Hash"));
                row.AddCell(new Cell(remoteHub.Hash ?? "Unknown"));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Enabled"));
                row.AddCell(new Cell(remoteHub.Enabled.ToString()));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Official"));
                row.AddCell(new Cell(remoteHub.Trusted.ToString()));
                list.Add(row);
            }
        }

        private void LocalHubList(MyGuiControlTable list, LocalHubConfig localHub)
        {
            {
                var row = new Row();
                row.AddCell(new Cell("Name"));
                row.AddCell(new Cell(localHub.Name));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Folder"));
                row.AddCell(new Cell(localHub.Folder));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Hash"));
                row.AddCell(new Cell(localHub.Hash ?? "Unknown"));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Enabled"));
                row.AddCell(new Cell(localHub.Enabled.ToString()));
                list.Add(row);
            }
        }

        private void RemotePluginList(MyGuiControlTable list, RemotePluginConfig remotePlugin)
        {
            {
                var row = new Row();
                row.AddCell(new Cell("Name"));
                row.AddCell(new Cell(remotePlugin.Name));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Repo"));
                row.AddCell(new Cell(remotePlugin.Repo));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Branch"));
                row.AddCell(new Cell(remotePlugin.Branch));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("File"));
                row.AddCell(new Cell(remotePlugin.File));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Last Check"));
                row.AddCell(new Cell(DateToString(remotePlugin.LastCheck)));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Enabled"));
                row.AddCell(new Cell(remotePlugin.Enabled.ToString()));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Official"));
                row.AddCell(new Cell(remotePlugin.Trusted.ToString()));
                list.Add(row);
            }
        }

        private void LocalPluginList(MyGuiControlTable list, LocalPluginConfig localPlugin)
        {
            {
                var row = new Row();
                row.AddCell(new Cell("Name"));
                row.AddCell(new Cell(localPlugin.Name));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Folder"));
                row.AddCell(new Cell(localPlugin.Folder));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("File"));
                row.AddCell(new Cell(localPlugin.File));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Enabled"));
                row.AddCell(new Cell(localPlugin.Enabled.ToString()));
                list.Add(row);
            }
        }

        private void ModList(MyGuiControlTable list, ModConfig mod)
        {
            {
                var row = new Row();
                row.AddCell(new Cell("Name"));
                row.AddCell(new Cell(mod.Name));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("ID"));
                row.AddCell(new Cell(mod.ID.ToString()));
                list.Add(row);
            }
            {
                var row = new Row();
                row.AddCell(new Cell("Enabled"));
                row.AddCell(new Cell(mod.Enabled.ToString()));
                list.Add(row);
            }
        }

        private string DateToString(DateTime? dateTime)
        {
            if (dateTime is DateTime dt)
                return dt.ToLocalTime().ToString("HH:mm:ss yyyy-MM-dd");

            return "Never";
        }

        private void PopulateList(MyGuiControlTable list)
        {
            list.Clear();
            list.Controls.Clear();

            if (Source is RemoteHubConfig remoteHub)
                RemoteHubList(list, remoteHub);
            else if (Source is LocalHubConfig localHub)
                LocalHubList(list, localHub);
            else if (Source is RemotePluginConfig remotePlugin)
                RemotePluginList(list, remotePlugin);
            else if (Source is LocalPluginConfig localPlugin)
                LocalPluginList(list, localPlugin);
            else if (Source is ModConfig mod)
                ModList(list, mod);
        }

        private void PromptRemoveSource()
        {
            string name = string.Empty;
            if (Source is RemoteHubConfig remoteHub)
                name = remoteHub.Name;
            else if (Source is LocalHubConfig localHub)
                name = localHub.Name;
            else if (Source is RemotePluginConfig remotePlugin)
                name = remotePlugin.Name;
            else if (Source is LocalPluginConfig localPlugin)
                name = localPlugin.Name;
            else if (Source is ModConfig mod)
                name = mod.Name;

            var msgBox = MyGuiSandbox.CreateMessageBox(
                messageCaption: new StringBuilder("Remove Source?"),
                messageText: new StringBuilder(
                    $"Are you sure you want to remove {name} from the list?"
                ),
                buttonType: MyMessageBoxButtonsType.YES_NO,
                callback: (x) =>
                {
                    if (x == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        CloseScreenNow();
                        OnRemove(Source);
                    }
                }
            );

            MyGuiSandbox.AddScreen(msgBox);
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
        }
    }
}
