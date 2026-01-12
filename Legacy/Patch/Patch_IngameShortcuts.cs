using System;
using HarmonyLib;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Screens;
using Pulsar.Shared;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Input;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch(typeof(MyGuiScreenGamePlay), "HandleUnhandledInput")]
public static class Patch_IngameShortcuts
{
    public static bool Prefix()
    {
        if (MySession.Static is null)
            return true;

        return !TryOpenQuickMenu(true);
    }

    public static bool TryOpenQuickMenu(bool allowConfig)
    {
        IMyInput input = MyInput.Static;
        if (input is null || !input.IsAnyAltKeyPressed() || !input.IsAnyCtrlKeyPressed())
            return false;

        if (input.IsNewKeyPressed(MyKeys.F5))
        {
            CreateDialog("Restart Space Engineers?", RestartCallback);
            return true;
        }

        if (allowConfig && input.IsNewKeyPressed(MyKeys.OemQuestion))
        {
            MyGuiSandbox.AddScreen(new ConfigurePlugin());
            return true;
        }

        if (input.IsNewKeyPressed(MyKeys.L))
        {
            CreateDialog("Show Space Engineers and Pulsar logs?", LogCallback);
            return true;
        }

        return false;
    }

    private static void CreateDialog(string text, Action<MyGuiScreenMessageBox.ResultEnum> callback)
    {
        var box = MyGuiSandbox.CreateMessageBox(
            MyMessageBoxStyleEnum.Error,
            MyMessageBoxButtonsType.YES_NO,
            new(text),
            new("Pulsar Quick Menu"),
            callback: callback
        );
        box.SkipTransition = true;
        box.CloseBeforeCallback = true;
        MyGuiSandbox.AddScreen(box);
    }

    private static void LogCallback(MyGuiScreenMessageBox.ResultEnum result)
    {
        if (result != MyGuiScreenMessageBox.ResultEnum.YES)
            return;

        LogFile.GameLog.Open();
        LogFile.Open();
    }

    private static void RestartCallback(MyGuiScreenMessageBox.ResultEnum result)
    {
        if (result != MyGuiScreenMessageBox.ResultEnum.YES)
            return;

        LoaderTools.AskToRestart();
    }
}
