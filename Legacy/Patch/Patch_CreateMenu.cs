using System.Text;
using HarmonyLib;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Screens;
using Pulsar.Shared;
using Sandbox.Graphics.GUI;
using SpaceEngineers.Game.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyGuiScreenMainMenu), "CreateMainMenu")]
public static class Patch_CreateMainMenu
{
    private static bool usedAutoRejoin = false;

    public static void Postfix(
        MyGuiScreenMainMenu __instance,
        Vector2 leftButtonPositionOrigin,
        ref Vector2 lastButtonPosition,
        MyGuiControlButton ___m_continueButton
    )
    {
        MyGuiControlButton lastBtn = null;
        foreach (var control in __instance.Controls)
        {
            if (control is MyGuiControlButton btn && btn.Position == lastButtonPosition)
            {
                lastBtn = btn;
                break;
            }
        }

        Vector2 position;
        if (lastBtn is null)
        {
            position = lastButtonPosition + MyGuiConstants.MENU_BUTTONS_POSITION_DELTA;
        }
        else
        {
            position = lastBtn.Position;
            lastBtn.Position = lastButtonPosition + MyGuiConstants.MENU_BUTTONS_POSITION_DELTA;
        }

        MyGuiControlButton openBtn = new(
            position,
            MyGuiControlButtonStyleEnum.StripeLeft,
            originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
            text: new StringBuilder("Plugins"),
            onButtonClick: _ => MainPluginMenu.Open()
        )
        {
            BorderEnabled = false,
            BorderSize = 0,
            BorderHighlightEnabled = false,
            BorderColor = Vector4.Zero,
        };
        __instance.Controls.Add(openBtn);

        if (
            ___m_continueButton is not null
            && ___m_continueButton.Visible
            && !usedAutoRejoin
            && Flags.ContinueGame
        )
        {
            ___m_continueButton.PressButton();
            usedAutoRejoin = true;
        }
    }
}

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyGuiScreenMainMenu), "CreateInGameMenu")]
public static class Patch_CreateInGameMenu
{
    public static void Postfix(
        MyGuiScreenMainMenu __instance,
        Vector2 leftButtonPositionOrigin,
        ref Vector2 lastButtonPosition
    )
    {
        Patch_CreateMainMenu.Postfix(
            __instance,
            leftButtonPositionOrigin,
            ref lastButtonPosition,
            null
        );
    }
}
