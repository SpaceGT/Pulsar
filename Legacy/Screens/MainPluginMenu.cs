using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Screens.Controls;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace Pulsar.Legacy.Screens;

public class MainPluginMenu(ConfigManager configManager) : PluginScreen(size: new Vector2(1, 0.9f))
{
    private Profile draft = Tools.DeepCopy(configManager.Profiles.Current);

    private readonly PluginList pluginList = configManager.List;
    private readonly ProfilesConfig profiles = configManager.Profiles;
    private readonly SourcesConfig sources = configManager.Sources;

    private MyGuiControlCheckbox consentBox;
    private MyGuiControlParent pluginsPanel;
    private MyGuiControlParent modsPanel;

    public static void Open()
    {
        var configManager = ConfigManager.Instance;
        MainPluginMenu menu = new(configManager);
        configManager.List.UpdateRemoteList();
        configManager.List.UpdateLocalList();
        MyGuiSandbox.AddScreen(menu);
    }

    public override string GetFriendlyName()
    {
        return typeof(MainPluginMenu).FullName;
    }

    public override void UnloadContent()
    {
        base.UnloadContent();
        PlayerConsent.OnConsentChanged -= OnConsentChanged;
    }

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);

        // Top
        MyGuiControlLabel caption = AddCaption("Pulsar", captionScale: 1);
        AddBarBelow(caption);

        // Bottom
        Vector2 bottomMid = new(0, m_size.Value.Y / 2);
        MyGuiControlButton btnApply = new(
            position: new Vector2(bottomMid.X - GuiSpacing, bottomMid.Y - GuiSpacing),
            text: new StringBuilder("Apply"),
            originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM,
            onButtonClick: OnApplyClick
        );
        MyGuiControlButton btnCancel = new(
            position: new Vector2(bottomMid.X + GuiSpacing, bottomMid.Y - GuiSpacing),
            text: new StringBuilder("Cancel"),
            originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM,
            onButtonClick: OnCancelClick
        );
        Controls.Add(btnApply);
        Controls.Add(btnCancel);
        AddBarAbove(btnApply);

        // Center
        var lblPlugins = new MyGuiControlLabel(text: "Plugins");

        MyLayoutTable grid = GetLayoutTableBetween(
            caption,
            btnApply,
            verticalSpacing: GuiSpacing * 2
        );
        grid.SetColumnWidthsNormalized(0.5f, 0.3f, 0.2f);
        grid.SetRowHeightsNormalized(0.05f, 0.95f);

        // Column 1
        grid.Add(lblPlugins, MyAlignH.Center, MyAlignV.Bottom, 0, 0);
        pluginsPanel = new MyGuiControlParent();
        grid.AddWithSize(pluginsPanel, MyAlignH.Center, MyAlignV.Center, 1, 0);
        CreatePluginsPanel(pluginsPanel, false);

        // Column 2
        grid.Add(new MyGuiControlLabel(text: "Mods"), MyAlignH.Center, MyAlignV.Bottom, 0, 1);
        modsPanel = new MyGuiControlParent();
        grid.AddWithSize(modsPanel, MyAlignH.Center, MyAlignV.Center, 1, 1);
        CreatePluginsPanel(modsPanel, true);

        // Column 3
        MyGuiControlParent sidePanel = new();
        grid.AddWithSize(sidePanel, MyAlignH.Center, MyAlignV.Center, 1, 2);
        CreateSidePanel(sidePanel);
    }

    private void CreatePluginsPanel(MyGuiControlParent parent, bool mods)
    {
        Vector2 topLeft = parent.Size * -0.5f;

        MyGuiControlButton btnAdd = new(
            visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.Increase,
            toolTip: mods ? "Add mod" : "Add plugin",
            originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
            onButtonClick: (x) => OpenAddPluginMenu(mods)
        );

        MyGuiControlTable list = CreatePluginTable(parent.Size, btnAdd.Size.Y, mods);
        parent.Controls.Add(list);

        btnAdd.Position = new Vector2(-topLeft.X, topLeft.Y + list.Size.Y);
        parent.Controls.Add(btnAdd);

        MyGuiControlButton btnOpen = new(
            size: btnAdd.Size,
            visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
            toolTip: "Show details",
            onButtonClick: OnPluginOpenClick
        )
        {
            UserData = list,
            Enabled = false,
        };
        PositionToLeft(btnAdd, btnOpen, spacing: GuiSpacing / 5);
        AddImageToButton(btnOpen, @"Textures\GUI\link.dds", 0.8f);
        parent.Controls.Add(btnOpen);
        list.ItemSelected += (list, args) =>
        {
            btnOpen.Enabled = TryGetListPlugin(list, args.RowIndex, out _);
        };

        if (!mods)
        {
            MyGuiControlButton btnSettings = new(
                size: btnAdd.Size,
                visualStyle: VRage.Game.MyGuiControlButtonStyleEnum.SquareSmall,
                toolTip: "Open plugin settings, in-game hotkey: Ctrl+Alt+/",
                onButtonClick: OnPluginSettingsClick
            )
            {
                UserData = list,
                Enabled = false,
            };
            PositionToLeft(btnOpen, btnSettings, spacing: GuiSpacing / 5);
            AddImageToButton(
                btnSettings,
                @"Textures\GUI\Controls\button_filter_system_highlight.dds",
                1
            );
            parent.Controls.Add(btnSettings);
            list.ItemSelected += (list, args) =>
            {
                btnSettings.Enabled =
                    TryGetListPlugin(list, args.RowIndex, out PluginData plugin)
                    && TryGetPluginInstance(plugin, out PluginInstance instance)
                    && instance.HasConfigDialog;
            };
        }

        list.ItemDoubleClicked += OnListItemDoubleClicked;
    }

    private void OpenAddPluginMenu(bool mods)
    {
        AddPluginMenu screen = new(pluginList, mods, draft);
        screen.Closed += Screen_Closed;
        MyGuiSandbox.AddScreen(screen);
    }

    private void Screen_Closed(MyGuiScreenBase source, bool isUnloading)
    {
        RefreshPluginLists();
        source.Closed -= Screen_Closed;
    }

    private void RefreshPluginLists()
    {
        pluginsPanel.Controls.Clear();
        CreatePluginsPanel(pluginsPanel, false);
        modsPanel.Controls.Clear();
        CreatePluginsPanel(modsPanel, true);
    }

    private void OnListItemDoubleClicked(MyGuiControlTable list, MyGuiControlTable.EventArgs args)
    {
        if (TryGetListPlugin(list, args.RowIndex, out PluginData plugin))
            OpenPluginDetails(plugin);
    }

    private void OnPluginSettingsClick(MyGuiControlButton btn)
    {
        if (
            btn.UserData is MyGuiControlTable list
            && TryGetListPlugin(list, out PluginData plugin)
            && TryGetPluginInstance(plugin, out PluginInstance instance)
        )
            instance.OpenConfig();
    }

    private void OnPluginOpenClick(MyGuiControlButton btn)
    {
        if (btn.UserData is MyGuiControlTable list && TryGetListPlugin(list, out PluginData plugin))
            OpenPluginDetails(plugin);
    }

    private bool TryGetPluginInstance(PluginData plugin, out PluginInstance instance)
    {
        return PluginLoader.Instance.TryGetPluginInstance(plugin.Id, out instance);
    }

    private void OpenPluginDetails(PluginData plugin)
    {
        PluginDetailMenu screen = new(plugin, draft);
        screen.Closed += Screen_Closed;
        MyGuiSandbox.AddScreen(screen);
    }

    private bool TryGetListPlugin(MyGuiControlTable list, out PluginData plugin)
    {
        MyGuiControlTable.Row row = list.SelectedRow;
        if (row is null)
        {
            plugin = null;
            return false;
        }

        plugin = row.UserData as PluginData;
        return plugin is not null;
    }

    private bool TryGetListPlugin(MyGuiControlTable list, int index, out PluginData plugin)
    {
        if (index >= 0 && index < list.RowsCount)
        {
            MyGuiControlTable.Row row = list.GetRow(index);
            plugin = row.UserData as PluginData;
            return plugin is not null;
        }

        plugin = null;
        return false;
    }

    private MyGuiControlTable CreatePluginTable(Vector2 parentSize, float bottomPadding, bool mods)
    {
        MyGuiControlTable list = new()
        {
            Position = parentSize * -0.5f, // Top left
            Size = new Vector2(parentSize.X, 0), // VisibleRowsCount controls y size
            OriginAlign = VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
        };

        SetTableHeight(list, parentSize.Y - bottomPadding);

        if (mods)
        {
            list.ColumnsCount = 2;
            list.SetCustomColumnWidths([0.7f, 0.3f]);
            list.SetColumnName(0, new StringBuilder("Name"));
            list.SetColumnName(1, new StringBuilder("Enabled"));
            list.SetColumnComparison(0, CellTextComparison);
        }
        else
        {
            list.ColumnsCount = 4;
            list.SetCustomColumnWidths([0.45f, 0.2f, 0.175f, 0.175f]);
            list.SetColumnName(0, new StringBuilder("Name"));
            list.SetColumnComparison(0, CellTextComparison);
            list.SetColumnName(1, new StringBuilder("Status"));
            list.SetColumnName(2, new StringBuilder("Version"));
            list.SetColumnName(3, new StringBuilder("Enabled"));
        }

        list.SortByColumn(0, MyGuiControlTable.SortStateEnum.Ascending, true);
        PopulateList(list, mods);
        return list;
    }

    #region Side Panel

    private void CreateSidePanel(MyGuiControlParent parent)
    {
        MyLayoutVertical layout = new(parent, 0);

        layout.Add(
            new MyGuiControlButton(
                text: new StringBuilder("Profiles"),
                toolTip: "Load or edit profiles",
                onButtonClick: OnProfilesClick
            ),
            MyAlignH.Center
        );
        AdvanceLayout(ref layout);

        MyGuiControlButton sourceButton = null;

        if (Flags.CustomSources)
            sourceButton = new MyGuiControlButton(
                text: new StringBuilder("Sources"),
                toolTip: "Add or remove plugin sources",
                onButtonClick: OnSourcesClick
            );
        else
            sourceButton = new MyGuiControlButton(
                text: new StringBuilder("Refresh"),
                toolTip: "Refresh all plugin sources",
                onButtonClick: (x) =>
                {
                    pluginList.UpdateRemoteList(force: true);
                    pluginList.UpdateLocalList();
                    sources.Save();
                    // Prevent network spam from non-technical users
                    sourceButton.Enabled = false;
                }
            );

        layout.Add(sourceButton, MyAlignH.Center);
        AdvanceLayout(ref layout);

        consentBox = new MyGuiControlCheckbox(
            toolTip: "Consent to use your data for usage tracking",
            isChecked: PlayerConsent.ConsentGiven
        );
        consentBox.IsCheckedChanged += OnConsentBoxChanged;
        PlayerConsent.OnConsentChanged += OnConsentChanged;
        layout.Add(consentBox, MyAlignH.Left);
        MyGuiControlLabel lblConsent = new(text: "Track Usage");
        PositionToRight(consentBox, lblConsent, spacing: 0);
        parent.Controls.Add(lblConsent);
    }

    private void OnConsentChanged()
    {
        UpdateConsentBox(consentBox);
    }

    private void OnConsentBoxChanged(MyGuiControlCheckbox checkbox)
    {
        PlayerConsent.ShowDialog();
        UpdateConsentBox(checkbox);
    }

    private void UpdateConsentBox(MyGuiControlCheckbox checkbox)
    {
        if (checkbox.IsChecked != PlayerConsent.ConsentGiven)
        {
            checkbox.IsCheckedChanged -= OnConsentBoxChanged;
            checkbox.IsChecked = PlayerConsent.ConsentGiven;
            checkbox.IsCheckedChanged += OnConsentBoxChanged;
        }
    }

    private void OnSourcesClick(MyGuiControlButton btn)
    {
        if (sources.ShowWarning)
        {
            SourcesWarning warning = new(
                sources,
                (state) =>
                {
                    if (state == true)
                        OpenSourcesMenu();
                }
            );
            MyGuiSandbox.AddScreen(warning);
        }
        else
            OpenSourcesMenu();
    }

    private void OpenSourcesMenu()
    {
        SourcesMenu screen = new(sources);
        screen.Closed += Screen_Closed;
        MyGuiSandbox.AddScreen(screen);
    }

    private void OnProfilesClick(MyGuiControlButton btn)
    {
        ProfilesMenu screen = new(draft);
        screen.OnDraftChange += ReplaceDraft;
        screen.Closed += Screen_Closed;
        MyGuiSandbox.AddScreen(screen);
    }
    #endregion

    private int CellTextComparison(MyGuiControlTable.Cell x, MyGuiControlTable.Cell y)
    {
        return TextComparison(x.Text, y.Text);
    }

    private static int TextComparison(StringBuilder x, StringBuilder y)
    {
        if (x is null)
        {
            if (y is null)
                return 0;
            return 1;
        }

        if (y is null)
            return -1;

        return x.CompareTo(y);
    }

    private void PopulateList(MyGuiControlTable list, bool mods)
    {
        list.Clear();
        list.Controls.Clear();
        foreach (PluginData plugin in pluginList.OrderBy(x => x.FriendlyName))
        {
            if (!IsEnabled(plugin))
                continue;

            if (plugin is ModPlugin)
            {
                if (!mods)
                    continue;
            }
            else
            {
                if (mods)
                    continue;
            }

            var tip = plugin.FriendlyName;
            if (!string.IsNullOrWhiteSpace(plugin.Tooltip))
                tip += "\n" + plugin.Tooltip;

            var row = new MyGuiControlTable.Row(plugin);

            row.AddCell(new MyGuiControlTable.Cell(plugin.FriendlyName, toolTip: tip));

            if (!mods)
            {
                string statusString = ConfigManager.Instance.SafeMode
                    ? "Disabled"
                    : plugin.StatusString;

                row.AddCell(new MyGuiControlTable.Cell(statusString, toolTip: tip));
                row.AddCell(
                    new MyGuiControlTable.Cell(plugin.Version?.ToString() ?? "N/A", toolTip: tip)
                );
            }

            var enabledCell = new MyGuiControlTable.Cell();
            var enabledCheckbox = new MyGuiControlCheckbox(isChecked: IsEnabled(plugin))
            {
                UserData = plugin,
                Visible = true,
            };
            enabledCheckbox.IsCheckedChanged += OnPluginCheckboxChanged;
            enabledCell.Control = enabledCheckbox;
            list.Controls.Add(enabledCheckbox);
            row.AddCell(enabledCell);

            list.Add(row);
        }

        if (list.RowsCount == 0)
        {
            var row = new MyGuiControlTable.Row();
            string helpText = "Click + below to install " + (mods ? "mods" : "plugins");
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

    private void OnPluginCheckboxChanged(MyGuiControlCheckbox checkbox)
    {
        if (checkbox.UserData is PluginData plugin)
            SetEnabled(plugin, checkbox.IsChecked);
    }

    #region Restart

    private bool IsEnabled(PluginData plugin)
    {
        return draft.Contains(plugin.Id);
    }

    private void ReplaceDraft(Profile profile)
    {
        SyncDevFolders(profile, draft);
        profile.Name = draft.Name;
        draft = profile;
    }

    private void SetEnabled(PluginData plugin, bool enabled)
    {
        plugin.UpdateProfile(draft, enabled);

        if (!enabled && plugin is LocalFolderPlugin devFolder)
            devFolder.DeserializeFile(null);

        RefreshPluginLists();
    }

    private void OnCancelClick(MyGuiControlButton btn)
    {
        SyncDevFolders(profiles.Current, draft);
        CloseScreen();
    }

    protected override void Canceling()
    {
        SyncDevFolders(profiles.Current, draft);
        base.Canceling();
    }

    private void OnApplyClick(MyGuiControlButton btn)
    {
        if (!SyncPluginConfigs())
        {
            CloseScreen();
            return;
        }

        foreach (string id in draft.GetPluginIDs())
            pluginList.SubscribeToItem(id);

        profiles.Current = draft;
        profiles.Save();

        MyGuiScreenMessageBox restartDialog = MyGuiSandbox.CreateMessageBox(
            MyMessageBoxStyleEnum.Info,
            MyMessageBoxButtonsType.YES_NO_CANCEL,
            new("A restart is required to apply changes. Would you like to restart the game now?"),
            new("Apply Changes?"),
            callback: AskRestartResult
        );

        MyGuiSandbox.AddScreen(restartDialog);
    }

    private void AskRestartResult(MyGuiScreenMessageBox.ResultEnum result)
    {
        if (result == MyGuiScreenMessageBox.ResultEnum.YES)
            LoaderTools.AskToRestart();
        else if (result == MyGuiScreenMessageBox.ResultEnum.NO)
            CloseScreen();
    }

    private bool SyncPluginConfigs()
    {
        Profile current = profiles.Current;
        bool hasDiff = false;

        foreach (string id in current.GetPluginIDs().Concat(draft.GetPluginIDs()))
        {
            PluginDataConfig cConfig = current.GetData(id);
            PluginDataConfig dConfig = draft.GetData(id);

            // Prebuilt and Mod plugins lack a config
            // FIXME: The diff check would have "just worked" if they did
            if (cConfig is null && dConfig is null)
            {
                hasDiff |= current.Local.Contains(id) != draft.Local.Contains(id);

                if (ulong.TryParse(id, out ulong wId))
                    hasDiff |= current.Mods.Contains(wId) != draft.Mods.Contains(wId);

                continue;
            }

            bool diff = cConfig is null || dConfig is null;

            if (cConfig is GitHubPluginConfig cGitHub && dConfig is GitHubPluginConfig dGitHub)
                diff |= cGitHub.SelectedVersion != dGitHub.SelectedVersion;

            if (cConfig is LocalFolderConfig cFolder && dConfig is LocalFolderConfig dFolder)
                diff |=
                    cFolder.DataFile != dFolder.DataFile
                    || cFolder.DebugBuild != dFolder.DebugBuild;

            if (diff && pluginList.TryGetPlugin(id, out PluginData plugin))
                plugin.LoadData(dConfig);

            hasDiff |= diff;
        }

        return hasDiff;
    }

    private void SyncDevFolders(Profile target, Profile previous)
    {
        IEnumerable<string> folderIDs = target
            .DevFolder.Concat(previous.DevFolder)
            .Select(c => c.Id);

        foreach (string configID in folderIDs)
        {
            var tFolder = (LocalFolderConfig)target.GetData(configID);
            var pFolder = (LocalFolderConfig)previous.GetData(configID);

            if (
                tFolder?.DataFile != pFolder?.DataFile
                && pluginList.TryGetPlugin(configID, out PluginData plugin)
            )
                plugin.LoadData(tFolder);
        }
    }

    #endregion

    public override void HandleUnhandledInput(bool receivedFocusInThisUpdate)
    {
        var input = VRage.Input.MyInput.Static;
        if (input.IsAnyAltKeyPressed() && input.IsAnyCtrlKeyPressed())
        {
            if (input.IsNewKeyPressed(VRage.Input.MyKeys.F5))
                Patch.Patch_IngameShortcuts.ShowRestartMenu();
            else if (input.IsNewKeyPressed(VRage.Input.MyKeys.L))
                Patch.Patch_IngameShortcuts.ShowLogMenu();
        }
    }
}
