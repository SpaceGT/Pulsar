using System;
using Sandbox;
using Sandbox.Graphics.GUI;
using VRage.Audio;
using VRage.Input;
using VRageMath;

namespace Pulsar.Legacy.Plugin.GUI.GuiControls
{
    class ParentButton : MyGuiControlParent
    {
        private bool mouseOver = false;
        private bool mouseClick = false;

        public event Action<ParentButton> OnButtonClicked;

        public ParentButton() { }

        public ParentButton(
            Vector2? position = null,
            Vector2? size = null,
            Vector4? backgroundColor = null,
            string toolTip = null
        )
            : base(position, size, backgroundColor, toolTip)
        {
            CanPlaySoundOnMouseOver = false;
            HighlightType = MyGuiControlHighlightType.WHEN_CURSOR_OVER;
            CanHaveFocus = true;
            IsActiveControl = true;
            OnMouseOverChanged(mouseOver);
        }

        public override MyGuiControlBase HandleInput()
        {
            bool actualMouseOver = CheckMouseOver(); // Do NOT trust Keen's IsMouseOver
            if (actualMouseOver != mouseOver)
            {
                mouseOver = actualMouseOver;
                OnMouseOverChanged(mouseOver);
            }

            if (mouseOver)
            {
                if (
                    MyInput.Static.IsNewPrimaryButtonPressed()
                    || MyInput.Static.IsNewSecondaryButtonPressed()
                )
                {
                    mouseClick = true;
                }
                else if (
                    mouseClick
                    && (
                        MyInput.Static.IsNewPrimaryButtonReleased()
                        || MyInput.Static.IsNewSecondaryButtonReleased()
                    )
                )
                {
                    mouseClick = false;
                    OnButtonClicked?.Invoke(this);
                }
            }
            else
            {
                mouseClick = false;
            }

            return base.HandleInput();
        }

        private void OnMouseOverChanged(bool mouseOver)
        {
            BorderEnabled = mouseOver;
            if (mouseOver)
                BackgroundTexture = MyGuiConstants.TEXTURE_RECTANGLE_NEUTRAL;
            else
                BackgroundTexture = MyGuiConstants.TEXTURE_RECTANGLE_DARK;
        }

        public void PlayClickSound()
        {
            MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
        }
    }
}
