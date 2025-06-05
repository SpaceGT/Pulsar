using Sandbox.Game.Screens.Helpers;
using Sandbox.Graphics.GUI;
using VRageMath;
using VRage.Utils;
using avaness.PluginLoader.Data;
using System.Collections.Generic;
using System;
using System.Linq;
using avaness.PluginLoader.Stats.Model;
using VRage.Game;
using avaness.PluginLoader.GUI.GuiControls;
using System.Diagnostics;
using System.IO;
using avaness.PluginLoader.Config;

namespace avaness.PluginLoader.GUI
{
    public class AddPluginMenu : PluginScreen
    {
        const int ListItemsHorizontal = 2;
        const int ListItemsVertical = 3;
        const float PercentSearchBox = 0.8f;

        private List<PluginData> plugins;
        private HashSet<string> enabledPlugins;
        private PluginStats stats;
        private bool mods;
        private MyGuiControlCombobox sortDropdown;
        private Vector2 pluginListSize;
        private MyGuiControlParent pluginListGrid;
        private string filter;

        public event Action OnRestartRequired;

        enum SortingMethod { Name, Usage, Rating }

        public AddPluginMenu(IEnumerable<PluginData> plugins, bool mods, HashSet<string> enabledPlugins) : base(size: new Vector2(0.8f, 0.9f))
        {
            this.plugins = plugins.Where(x => (x is ModPlugin) == mods).ToList();
            stats = Main.Instance.Stats ?? new PluginStats();
            this.mods = mods;
            this.enabledPlugins = enabledPlugins;
            SortPlugins(SortingMethod.Name);
        }

        public override string GetFriendlyName()
        {
            return typeof(AddPluginMenu).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            // Top
            MyGuiControlLabel caption = AddCaption(mods ? "Mod List" : "Plugin List", captionScale: 1);
            AddBarBelow(caption);

            // Center 
            float width = m_size.Value.X - (GuiSpacing * 2);
            Vector2 halfSize = m_size.Value / 2;
            Vector2 searchPos = new Vector2(GuiSpacing - halfSize.X, GetCoordTopLeftFromAligned(caption).Y + caption.Size.Y + (GuiSpacing * 2));
            MyGuiControlSearchBox searchBox = new MyGuiControlSearchBox(position: searchPos, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            searchBox.Size = new Vector2(width * PercentSearchBox, searchBox.Size.Y);
            searchBox.OnTextChanged += SearchBox_OnTextChanged;
            Controls.Add(searchBox);

            Vector2 sortPos = new Vector2(searchPos.X + searchBox.Size.X + GuiSpacing, searchPos.Y);
            Vector2 sortSize = new Vector2((width * (1 - PercentSearchBox)) - GuiSpacing, searchBox.Size.Y);
            MyGuiControlCombobox dropdown = new MyGuiControlCombobox(position: sortPos, size: sortSize, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            dropdown.AddItem(-1, "Sort By");
            string[] sortMethods = Enum.GetNames(typeof(SortingMethod));
            for(int i = 0; i < sortMethods.Length; i++)
                dropdown.AddItem(i, sortMethods[i]);
            dropdown.SelectItemByKey(-1);
            dropdown.ItemSelected += OnSortSelected;
            Controls.Add(dropdown);
            sortDropdown = dropdown;

            Vector2 areaPosition = searchPos + new Vector2(0, searchBox.Size.Y + GuiSpacing);
            Vector2 areaSize = new Vector2(width, Math.Abs(areaPosition.Y - halfSize.Y) - GuiSpacing);

            MyGuiControlParent gridArea = new MyGuiControlParent(position: areaPosition)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP
            };
            MyGuiControlScrollablePanel scrollPanel = new MyGuiControlScrollablePanel(gridArea)
            {
                BackgroundTexture = MyGuiConstants.TEXTURE_SCROLLABLE_LIST,
                BorderHighlightEnabled = true,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Position = areaPosition,
                Size = areaSize,
                ScrollbarVEnabled = true,
                CanFocusChildren = true,
                ScrolledAreaPadding = new MyGuiBorderThickness(0.005f),
                DrawScrollBarSeparator = true,
            };
            gridArea.Position = areaPosition;
            pluginListSize = areaSize - (scrollPanel.ScrolledAreaPadding.SizeChange + new Vector2(scrollPanel.ScrollbarVSizeX, 0));
            CreatePluginList(gridArea);
            Controls.Add(scrollPanel);
            pluginListGrid = gridArea;

            FocusedControl = searchBox.TextBox;
        }

        private void SearchBox_OnTextChanged(string newText)
        {
            filter = newText;
            RefreshPluginList();
        }

        private void OnSortSelected()
        {
            int selectedItem = (int)sortDropdown.GetSelectedKey();
            if(Enum.IsDefined(typeof(SortingMethod), selectedItem))
            {
                if(sortDropdown.TryGetItemByKey(-1) != null)
                {
                    // In order to remove the placeholder without messing up the dropdown highlight, the selected item must be selected again
                    sortDropdown.ItemSelected -= OnSortSelected;
                    sortDropdown.SelectItemByKey(-1);
                    sortDropdown.RemoveItem(-1);
                    sortDropdown.SelectItemByKey(selectedItem);
                    sortDropdown.ItemSelected += OnSortSelected;
                }
                SortPlugins((SortingMethod)selectedItem);
                RefreshPluginList();
            }
        }
        
        private void SortPlugins(SortingMethod sort)
        {
            switch (sort)
            {
                case SortingMethod.Name:
                    plugins.Sort(ComparePluginsByName);
                    break;
                case SortingMethod.Usage:
                    plugins.Sort(ComparePluginsByUsage);
                    break;
                case SortingMethod.Rating:
                    plugins.Sort(ComparePluginsByRating);
                    break;
                default:
                    plugins.Sort(ComparePluginsByName);
                    break;
            }
        }

        private int ComparePluginsByName(PluginData x, PluginData y)
        {
            return x.FriendlyName.CompareTo(y.FriendlyName);
        }

        private int ComparePluginsByUsage(PluginData x, PluginData y)
        {
            PluginStat statX = stats.GetStatsForPlugin(x);
            PluginStat statY = stats.GetStatsForPlugin(y);
            int usage = -statX.Players.CompareTo(statY.Players);
            if (usage != 0)
                return usage;
            return ComparePluginsByName(x, y);
        }

        private int ComparePluginsByRating(PluginData x, PluginData y)
        {
            PluginStat statX = stats.GetStatsForPlugin(x);
            int ratingX = statX.Upvotes - statX.Downvotes;
            PluginStat statY = stats.GetStatsForPlugin(y);
            int ratingY = statY.Upvotes - statY.Downvotes;
            int rating = -ratingX.CompareTo(ratingY);
            if (rating != 0)
                return rating;
            return ComparePluginsByName(x, y);
        }

        private void RefreshPluginList()
        {
            pluginListGrid.Controls.Clear();
            CreatePluginList(pluginListGrid);
        }

        private IEnumerable<PluginData> GetFilteredPlugins()
        {
            if (string.IsNullOrWhiteSpace(filter))
                return plugins.Where(x => !x.Hidden);
            string[] splitFilter = filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Plugin name must contain every item from the filter
            return plugins.Where(plugin => splitFilter.All(arg => plugin.FriendlyName.Contains(arg, StringComparison.OrdinalIgnoreCase)));
        }

        private void CreatePluginList(MyGuiControlParent panel)
        {
            PluginData[] plugins = GetFilteredPlugins().ToArray();

            Vector2 itemSize = pluginListSize / new Vector2(ListItemsHorizontal, ListItemsVertical);
            int numPlugins = plugins.Length;
            if (!mods && string.IsNullOrWhiteSpace(filter))
                numPlugins += 2;
            int totalRows = (int)Math.Ceiling(numPlugins / (float)ListItemsHorizontal);
            panel.Size = new Vector2(pluginListSize.X, itemSize.Y * totalRows);

            Vector2 itemPositionOffset = (itemSize / 2) - (panel.Size / 2);

            for (int i = 0; i < numPlugins; i++)
            {
                int row = i / ListItemsHorizontal;
                int col = i % ListItemsHorizontal;
                Vector2 itemPosition = (itemSize * new Vector2(col, row)) + itemPositionOffset;
                MyGuiControlParent itemPanel = new MyGuiControlParent(position: itemPosition, size: itemSize);

                if (i < plugins.Length)
                    CreatePluginListItem(plugins[i], itemPanel);

                panel.Controls.Add(itemPanel);
            }
        }

        private void CreatePluginListItem(PluginData plugin, MyGuiControlParent panel)
        {
            float padding = GuiSpacing;

            ParentButton contentArea = new ParentButton(size: panel.Size - padding)
            {
                UserData = plugin,
            };
            contentArea.OnButtonClicked += OnPluginItemClicked;

            Vector2 contentTopLeft = GetCoordTopLeftFromAligned(contentArea) + padding;
            Vector2 contentSize = contentArea.Size - (padding * 2);

            MyLayoutTable layout = new MyLayoutTable(contentArea, contentTopLeft, contentSize);
            layout.SetColumnWidthsNormalized(0.5f, 0.5f);
            layout.SetRowHeightsNormalized(0.1f, 0.1f, 0.6f, 0.1f, 0.1f);

            MyGuiControlLabel titleLabel = new MyGuiControlLabel(text: plugin.FriendlyName, textScale: 0.9f);
            layout.Add(titleLabel, MyAlignH.Left, MyAlignV.Bottom, 0, 0);
            layout.Add(new MyGuiControlLabel(text: plugin.Author), MyAlignH.Left, MyAlignV.Top, 1, 0);

            MyGuiControlMultilineText description = new MyGuiControlMultilineText(textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
            {
                VisualStyle = MyGuiControlMultilineStyleEnum.Default,
                Visible = true,
                CanPlaySoundOnMouseOver = false,
            };
            layout.AddWithSize(description, MyAlignH.Left, MyAlignV.Top, 2, 0, 1, 2);
            if (string.IsNullOrEmpty(plugin.Tooltip))
            {
                string shortDescription = plugin.Description;
                if(!string.IsNullOrEmpty(shortDescription))
                {
                    shortDescription = shortDescription.TrimStart();
                    int firstEnter = shortDescription.IndexOf('\n');
                    if (firstEnter < 0)
                        firstEnter = shortDescription.Length - 1;
                    if (firstEnter > 120)
                        shortDescription = shortDescription.Substring(0, 117) + "...";
                    else
                        shortDescription = shortDescription.Substring(0, firstEnter + 1);
                    description.AppendText(shortDescription);
                }
            }
            else
            {
                description.AppendText(plugin.Tooltip);
            }


            if (!plugin.IsLocal)
            {
                PluginStat stat = stats.GetStatsForPlugin(plugin);
                layout.Add(new MyGuiControlLabel(text: stat.Players + " users"), MyAlignH.Left, MyAlignV.Bottom, 3, 0);

                MyGuiControlParent votingPanel = new MyGuiControlParent();
                layout.AddWithSize(votingPanel, MyAlignH.Center, MyAlignV.Center, 3, 1, 2);
                CreateVotingPanel(votingPanel, stat);
            }

            layout.Add(new MyGuiControlLabel(text: plugin.Source), MyAlignH.Left, MyAlignV.Bottom, 4, 0);

            MyGuiControlCheckbox enabledCheckbox = new MyGuiControlCheckbox(position: contentTopLeft + new Vector2(contentSize.X, 0), originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, isChecked: enabledPlugins.Contains(plugin.Id))
            {
                Name = "PluginEnabled",
                UserData = plugin,
            };
            enabledCheckbox.IsCheckedChanged += OnEnabledChanged;
            contentArea.Controls.Add(enabledCheckbox);

            float titleMaxWidth = Math.Abs(GetCoordTopLeftFromAligned(titleLabel).X - GetCoordTopLeftFromAligned(enabledCheckbox).X) - GuiSpacing;
            titleLabel.IsAutoEllipsisEnabled = true;
            titleLabel.SetMaxSize(new Vector2(titleMaxWidth, titleLabel.Size.Y));
            titleLabel.DoEllipsisAndScaleAdjust();

            panel.Controls.Add(contentArea);
        }

        private void OnEnabledChanged(MyGuiControlCheckbox checkbox)
        {
            if(checkbox.UserData is PluginData plugin)
            {
                if (checkbox.IsChecked)
                    enabledPlugins.Add(plugin.Id);
                else
                    enabledPlugins.Remove(plugin.Id);

                if (plugin.UpdateEnabledPlugins(enabledPlugins, checkbox.IsChecked))
                    RefreshPluginList();
            }
        }

        private void OnPluginItemClicked(ParentButton btn)
        {
            MyGuiControlBase checkbox = btn.Controls.GetControlByName("PluginEnabled");
            if (checkbox != null && checkbox.CheckMouseOver(false))
                return;
            if (btn.UserData is PluginData plugin)
            {
                btn.PlayClickSound();
                PluginDetailMenu screen = new PluginDetailMenu(plugin, enabledPlugins);
                screen.OnRestartRequired += DetailMenu_OnRestartRequired;
                screen.OnPluginRemoved += DetailMenu_OnPluginRemoved;
                screen.Closed += DetailMenu_Closed;
                MyScreenManager.AddScreen(screen);
            }
        }

        private void DetailMenu_OnRestartRequired()
        {
            OnRestartRequired?.Invoke();
        }

        private void DetailMenu_OnPluginRemoved(PluginData plugin)
        {
            int index = plugins.FindIndex(x => x.Id == plugin.Id);
            if (index >= 0)
                plugins.RemoveAt(index);
        }

        private void DetailMenu_Closed(MyGuiScreenBase source, bool isUnloading)
        {
            stats = Main.Instance.Stats ?? new PluginStats(); // Just in case it was null/empty before
            RefreshPluginList();
            source.Closed -= DetailMenu_Closed;
        }

        private void CreateVotingPanel(MyGuiControlParent parent, PluginStat stats)
        {
            MyLayoutHorizontal layout = new MyLayoutHorizontal(parent, 0);

            float height = parent.Size.Y;
            float width = (height * MyGuiConstants.GUI_OPTIMAL_SIZE.Y) / MyGuiConstants.GUI_OPTIMAL_SIZE.X;
            Vector2 size = new Vector2(width, height) * 0.8f;

            MyGuiControlImage imgVoteUp = new MyGuiControlImage(size: size, textures: new[] { @"Textures\GUI\Icons\Blueprints\like_test.png" });
            layout.Add(imgVoteUp, MyAlignV.Center);

            MyGuiControlLabel lblVoteUp = new MyGuiControlLabel(text: stats.Upvotes.ToString());
            PositionToRight(imgVoteUp, lblVoteUp, spacing: GuiSpacing / 5);
            AdvanceLayout(ref layout, lblVoteUp.Size.X + GuiSpacing);
            parent.Controls.Add(lblVoteUp);

            MyGuiControlImage imgVoteDown = new MyGuiControlImage(size: size, textures: new[] { @"Textures\GUI\Icons\Blueprints\dislike_test.png" });
            layout.Add(imgVoteDown, MyAlignV.Center);

            MyGuiControlLabel lblVoteDown = new MyGuiControlLabel(text: stats.Downvotes.ToString());
            PositionToRight(imgVoteDown, lblVoteDown, spacing: GuiSpacing / 5);
            parent.Controls.Add(lblVoteDown);
        }
    }
}
