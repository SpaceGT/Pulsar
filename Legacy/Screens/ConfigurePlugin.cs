using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar.Legacy.Loader;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace Pulsar.Legacy.Screens;

public class ConfigurePlugin : PluginScreen
{
    private readonly List<PluginInstance> pluginInstances;

    private MyGuiControlTable table;

    public override string GetFriendlyName()
    {
        return typeof(PluginDetailMenu).FullName;
    }

    public ConfigurePlugin()
        : base(size: new Vector2(0.7f, 0.9f))
    {
        pluginInstances = [.. PluginLoader.Instance.Plugins.Where(p => p.HasConfigDialog)];
    }

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);

        MyGuiControlLabel caption = AddCaption("Configure a plugin", captionScale: 1);
        AddBarBelow(caption);

        RectangleF area = GetAreaBelow(caption, GuiSpacing * 2);
        table = new MyGuiControlTable
        {
            Size = area.Size,
            Position = area.Position,
            OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
            ColumnsCount = 1,
        };
        table.SetCustomColumnWidths([1.0f]);
        table.SetColumnName(0, new StringBuilder("Name"));
        table.SetColumnComparison(0, CellTextComparison);

        table.ItemDoubleClicked += OnItemSelected;
        table.ItemSelected += OnItemSelected;

        SetTableHeight(table, area.Height - GuiSpacing);

        AddTableRows();
        table.SortByColumn(0, MyGuiControlTable.SortStateEnum.Ascending);
        table.SelectedRowIndex = -1;

        Controls.Add(table);
    }

    private void AddTableRows()
    {
        foreach (PluginInstance p in pluginInstances)
        {
            if (p.HasConfigDialog)
                table.Add(CreateRow(p));
        }
    }

    private static MyGuiControlTable.Row CreateRow(PluginInstance pluginInstance)
    {
        MyGuiControlTable.Row row = new(pluginInstance);
        row.AddCell(
            new MyGuiControlTable.Cell(
                text: pluginInstance?.FriendlyName ?? "",
                userData: pluginInstance
            )
        );
        return row;
    }

    private int CellTextComparison(MyGuiControlTable.Cell x, MyGuiControlTable.Cell y)
    {
        if (x is null)
            return y is null ? 0 : 1;

        return y is null ? -1 : TextComparison(x.Text, y.Text);
    }

    private int TextComparison(StringBuilder x, StringBuilder y)
    {
        if (x is null)
            return y is null ? 0 : 1;

        return y is null ? -1 : x.CompareTo(y);
    }

    private void OnItemSelected(MyGuiControlTable arg1, MyGuiControlTable.EventArgs arg2)
    {
        if (table.SelectedRow?.GetCell(0).UserData is not PluginInstance selectedPluginInstance)
            return;

        selectedPluginInstance.OpenConfig();
        CloseScreen();
    }
}
