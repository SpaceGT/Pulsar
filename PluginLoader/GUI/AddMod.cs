﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using avaness.PluginLoader.Config;
using avaness.PluginLoader.Data;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace avaness.PluginLoader.GUI.GuiControls
{
    internal class AddMod : PluginScreen
    {
        private Action<ModConfig> AddSource;

        private MyGuiControlTextbox NameInput;
        private MyGuiControlTextbox IdInput;

        private MyGuiControlButton CancelButton;
        private MyGuiControlButton AddButton;

        public AddMod(Action<ModConfig> onAdd)
            : base(size: new Vector2(0.5f, 0.36f))
        {
            AddSource = onAdd;
        }

        public override string GetFriendlyName()
        {
            return typeof(AddMod).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            MyGuiControlLabel caption = AddCaption("Mod Source", captionScale: 1.2f);
            AddBarBelow(caption);

            Vector2 bottomMid = new Vector2(0, m_size.Value.Y / 2);

            CancelButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X - GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Cancel"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => CloseScreen()
            );
            AddButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X + GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Add"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => OnAddClick()
            );

            Controls.Add(AddButton);
            Controls.Add(CancelButton);

            float vPadding = 0;

            MyGuiControlLabel nameLabel = new MyGuiControlLabel(
                position: new Vector2(0, caption.PositionY + caption.Size.Y + 1.5f * GuiSpacing),
                text: "Display Name",
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            );
            NameInput = new MyGuiControlTextbox(
                position: new Vector2(
                    nameLabel.PositionX,
                    nameLabel.PositionY + nameLabel.Size.Y + GuiSpacing
                )
            );
            Controls.Add(nameLabel);
            Controls.Add(NameInput);

            MyGuiControlLabel idLabel = new MyGuiControlLabel(
                position: new Vector2(
                    0,
                    NameInput.PositionY + NameInput.Size.Y + GuiSpacing + vPadding
                ),
                text: "Steam ID",
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            );
            IdInput = new MyGuiControlTextbox(
                position: new Vector2(
                    idLabel.PositionX,
                    idLabel.PositionY + idLabel.Size.Y + GuiSpacing
                )
            )
            {
                Type = MyGuiControlTextboxType.DigitsOnly,
            };
            Controls.Add(idLabel);
            Controls.Add(IdInput);

            string clipboard = Tools.Tools.GetClipboard();
            if (!string.IsNullOrEmpty(clipboard))
            {
                string[] parts = clipboard.Split('/');
                if (parts.Length == 3 && parts[0] == "semod")
                {
                    NameInput.Text = parts[1];
                    IdInput.Text = parts[2];
                }
            }
        }

        private void OnAddClick()
        {
            if (IdInput.Text.Length == 0)
            {
                return;
            }

            ModConfig source = new ModConfig()
            {
                Name = NameInput.Text,
                ID = long.Parse(IdInput.Text),
                Enabled = true,
            };

            AddSource(source);
            CloseScreen();
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
        }
    }
}
