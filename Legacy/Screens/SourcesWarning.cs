using System;
using System.Text;
using Pulsar.Shared.Config;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace Pulsar.Legacy.Screens.Controls
{
    internal class SourcesWarning(SourcesConfig config, Action<bool> onClose)
        : PluginScreen(size: new Vector2(0.5f, 0.4f))
    {
        private readonly Action<bool> CloseFunc = onClose;

        private readonly SourcesConfig Config = config;
        private bool ShowWarning = config.ShowWarning;

        private MyGuiControlButton CancelButton;
        private MyGuiControlButton ContinueButton;

        private static readonly StringBuilder Text = new(
            "You may modify the sources used when searching for plugins.\n"
                + "Please note plugins can execute arbitrary code.\n"
                + "(Adding unknown sources is a SECURITY RISK!)\n\n"
                + "Please make sure you trust the source before proceeding.\n"
                + "Do you want to continue?"
        );

        public override string GetFriendlyName()
        {
            return typeof(SourcesWarning).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            MyGuiControlLabel caption = AddCaption("External Sources Warning", captionScale: 1.2f);
            AddBarBelow(caption);

            Vector2 bottomMid = new(0, m_size.Value.Y / 2);

            CancelButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X - GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Cancel"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) =>
                {
                    CloseScreen();
                    CloseFunc(false);
                }
            );
            ContinueButton = new MyGuiControlButton(
                position: new Vector2(bottomMid.X + GuiSpacing, bottomMid.Y - GuiSpacing),
                text: new StringBuilder("Acknowledge"),
                originAlign: VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM,
                onButtonClick: (x) => OnContinueClick()
            );

            Controls.Add(ContinueButton);
            Controls.Add(CancelButton);

            MyGuiControlMultilineText text = new()
            {
                Text = Text,
                Size = new Vector2(m_size.Value.X, 0.5f * m_size.Value.Y),
                Position = new Vector2(0, caption.Position.Y + 0.5f * caption.Size.Y),
                TextAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP,
            };

            Controls.Add(text);

            MyGuiControlCheckbox checkbox = new(
                isChecked: !ShowWarning,
                position: new Vector2(
                    -0.05f,
                    ContinueButton.PositionY - ContinueButton.Size.Y - GuiSpacing * 2
                ),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM
            )
            {
                Visible = true,
            };

            MyGuiControlLabel label = new(
                text: "Don't Show Again",
                position: new Vector2(
                    checkbox.PositionX,
                    checkbox.PositionY - 0.3f * checkbox.Size.Y
                ),
                size: new Vector2(0.3f, checkbox.Size.Y),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM
            );

            checkbox.IsCheckedChanged += (x) => OnCheckboxClick(x, label);

            Controls.Add(label);
            Controls.Add(checkbox);
        }

        private void OnCheckboxClick(MyGuiControlCheckbox checkbox, MyGuiControlLabel label)
        {
            checkbox.Enabled = false;
            label.ColorMask = new Vector4(1, 1, 1, 0.5f);
            ShowWarning = !checkbox.IsChecked;
        }

        private void OnContinueClick()
        {
            if (Config.ShowWarning != ShowWarning)
            {
                Config.ShowWarning = ShowWarning;
                Config.Save();
            }

            CloseScreen();
            CloseFunc(true);
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
        }
    }
}
