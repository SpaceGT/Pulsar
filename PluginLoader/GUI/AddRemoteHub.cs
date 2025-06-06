﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using avaness.PluginLoader.Config;
using avaness.PluginLoader.Data;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Utils;
using VRageMath;

namespace avaness.PluginLoader.GUI
{
    internal class AddRemoteHub : PluginScreen
    {
        private Action<RemoteHubConfig> AddSource;

        private MyGuiControlTextbox NameInput;
        private MyGuiControlTextbox UserInput;
        private MyGuiControlTextbox RepoInput;
        private MyGuiControlTextbox BranchInput;

        private MyGuiControlButton CancelButton;
        private MyGuiControlButton AddButton;

        public AddRemoteHub(Action<RemoteHubConfig> onAdd)
            : base(size: new Vector2(0.5f, 0.54f))
        {
            AddSource = onAdd;
        }

        public override string GetFriendlyName()
        {
            return typeof(AddRemoteHub).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            MyGuiControlLabel caption = AddCaption("Remote Hub Source", captionScale: 1.2f);
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

            MyGuiControlLabel userLabel = new MyGuiControlLabel(
                position: new Vector2(
                    0,
                    NameInput.PositionY + NameInput.Size.Y + GuiSpacing + vPadding
                ),
                text: "GitHub User",
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            );
            UserInput = new MyGuiControlTextbox(
                position: new Vector2(
                    userLabel.PositionX,
                    userLabel.PositionY + userLabel.Size.Y + GuiSpacing
                )
            );
            Controls.Add(userLabel);
            Controls.Add(UserInput);

            MyGuiControlLabel repoLabel = new MyGuiControlLabel(
                position: new Vector2(
                    0,
                    UserInput.PositionY + UserInput.Size.Y + GuiSpacing + vPadding
                ),
                text: "Repo Name",
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            );
            RepoInput = new MyGuiControlTextbox(
                position: new Vector2(
                    repoLabel.PositionX,
                    repoLabel.PositionY + repoLabel.Size.Y + GuiSpacing
                )
            );
            Controls.Add(repoLabel);
            Controls.Add(RepoInput);

            MyGuiControlLabel branchLabel = new MyGuiControlLabel(
                position: new Vector2(
                    0,
                    RepoInput.PositionY + RepoInput.Size.Y + GuiSpacing + vPadding
                ),
                text: "Branch Name",
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
            );
            BranchInput = new MyGuiControlTextbox(
                position: new Vector2(
                    branchLabel.PositionX,
                    branchLabel.PositionY + repoLabel.Size.Y + GuiSpacing
                ),
                defaultText: "main"
            );
            Controls.Add(branchLabel);
            Controls.Add(BranchInput);

            string clipboard = Tools.Tools.GetClipboard();
            if (!string.IsNullOrEmpty(clipboard))
            {
                string[] parts = clipboard.Split('/');
                if (parts.Length == 4 && parts[0] == "sehub")
                {
                    NameInput.Text = parts[1] + "/" + parts[2];
                    UserInput.Text = parts[1];
                    RepoInput.Text = parts[2];
                    BranchInput.Text = parts[3];
                }
            }
        }

        private void OnAddClick()
        {
            RemoteHubConfig source = new RemoteHubConfig()
            {
                Name = NameInput.Text,
                Repo = UserInput.Text + "/" + RepoInput.Text,
                Branch = BranchInput.Text,
                LastCheck = null,
                Hash = null,
                Enabled = true,
                Trusted = false
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
