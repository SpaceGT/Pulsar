using System;
using System.Text;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace Pulsar.Legacy.Screens;

internal class AddRemotePlugin(Action<RemotePluginConfig> onAdd)
    : PluginScreen(size: new Vector2(0.5f, 0.54f))
{
    private readonly Action<RemotePluginConfig> AddSource = onAdd;

    private MyGuiControlTextbox FileInput;
    private MyGuiControlTextbox UserInput;
    private MyGuiControlTextbox RepoInput;
    private MyGuiControlTextbox BranchInput;

    private MyGuiControlButton CancelButton;
    private MyGuiControlButton AddButton;

    public override string GetFriendlyName()
    {
        return typeof(AddRemoteHub).FullName;
    }

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);

        MyGuiControlLabel caption = AddCaption("Remote Plugin Source", captionScale: 1.2f);
        AddBarBelow(caption);

        Vector2 bottomMid = new(0, m_size.Value.Y / 2);

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

        MyGuiControlLabel userLabel = new(
            position: new Vector2(0, caption.PositionY + caption.Size.Y + 1.5f * GuiSpacing),
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

        MyGuiControlLabel repoLabel = new(
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

        MyGuiControlLabel branchLabel = new(
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

        MyGuiControlLabel fileLabel = new(
            position: new Vector2(
                0,
                BranchInput.PositionY + BranchInput.Size.Y + GuiSpacing + vPadding
            ),
            text: "Metadata File",
            originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP
        );
        FileInput = new MyGuiControlTextbox(
            position: new Vector2(
                fileLabel.PositionX,
                fileLabel.PositionY + fileLabel.Size.Y + GuiSpacing
            ),
            defaultText: "PluginHub.xml"
        );
        Controls.Add(fileLabel);
        Controls.Add(FileInput);

        string clipboard = Tools.GetClipboard();
        if (!string.IsNullOrEmpty(clipboard))
        {
            string[] parts = clipboard.Split('/');
            if (parts.Length == 5 && parts[0] == "seplugin")
            {
                UserInput.Text = parts[1];
                RepoInput.Text = parts[2];
                BranchInput.Text = parts[3];
                FileInput.Text = parts[4];
            }
        }
    }

    private void OnAddClick()
    {
        RemotePluginConfig source = new()
        {
            Name = RepoInput.Text,
            Repo = UserInput.Text + "/" + RepoInput.Text,
            Branch = BranchInput.Text,
            File = FileInput.Text,
            LastCheck = null,
            Enabled = true,
            Trusted = false,
        };

        AddSource(source);
        CloseScreen();
    }

    public override void OnRemoved()
    {
        base.OnRemoved();
    }
}
