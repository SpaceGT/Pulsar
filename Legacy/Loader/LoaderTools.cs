using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using HarmonyLib;
using Pulsar.Shared;
using Sandbox;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Audio;
using VRage.FileSystem;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Loader;

public static class LoaderTools
{
    public const string ContinueArg = "-continue";
    public const string DebugArg = "-debug";

    public static void AskToRestart()
    {
        if (MyGuiScreenGamePlay.Static is not null)
            AskSave(
                delegate
                {
                    Unload();
                    Restart();
                }
            );
        else
        {
            Unload();
            Restart();
        }
    }

    /// <summary>
    /// From WesternGamer/InGameWorldLoading
    /// </summary>
    /// <param name="afterMenu">Action after code is executed.</param>
    private static void AskSave(Action afterMenu)
    {
        // Sync.IsServer is backwards
        if (!Sync.IsServer)
        {
            afterMenu();
            return;
        }

        string message = "";
        bool isCampaign = false;
        MyMessageBoxButtonsType buttonsType = MyMessageBoxButtonsType.YES_NO_CANCEL;

        // Sync.IsServer is backwards
        if (Sync.IsServer && !MySession.Static.Settings.EnableSaving)
        {
            message +=
                "Are you sure that you want to restart the game? All progress from the last checkpoint will be lost.";
            isCampaign = true;
            buttonsType = MyMessageBoxButtonsType.YES_NO;
        }
        else
        {
            message += "Save changes before restarting game?";
        }

        MyGuiScreenMessageBox saveMenu = MyGuiSandbox.CreateMessageBox(
            buttonType: buttonsType,
            messageText: new StringBuilder(message),
            messageCaption: MyTexts.Get(MyCommonTexts.MessageBoxCaptionPleaseConfirm),
            callback: ShowSaveMenuCallback,
            cancelButtonText: MyStringId.GetOrCompute("Don't Restart")
        );
        saveMenu.InstantClose = false;
        MyGuiSandbox.AddScreen(saveMenu);

        void ShowSaveMenuCallback(MyGuiScreenMessageBox.ResultEnum callbackReturn)
        {
            if (isCampaign)
            {
                if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                    afterMenu();

                return;
            }

            switch (callbackReturn)
            {
                case MyGuiScreenMessageBox.ResultEnum.YES:
                    MyAsyncSaving.Start(
                        delegate
                        {
                            MySandboxGame.Static.OnScreenshotTaken +=
                                UnloadAndExitAfterScreenshotWasTaken;
                        }
                    );
                    break;

                case MyGuiScreenMessageBox.ResultEnum.NO:
                    MyAudio.Static.Mute = true;
                    MyAudio.Static.StopMusic();
                    afterMenu();
                    break;
                case MyGuiScreenMessageBox.ResultEnum.CANCEL:
                    break;
                default:
                    break;
            }
        }

        void UnloadAndExitAfterScreenshotWasTaken(object sender, EventArgs e)
        {
            MySandboxGame.Static.OnScreenshotTaken -= UnloadAndExitAfterScreenshotWasTaken;
            afterMenu();
        }
    }

    public static void Unload()
    {
        LogFile.Dispose();
        MySessionLoader.Unload();
        MySandboxGame.Config.ControllerDefaultOnStart = MyInput.Static.IsJoystickLastUsed;
        MySandboxGame.Config.Save();
        MyScreenManager.CloseAllScreensNowExcept(null);
        MyPlugins.Unload();
    }

    public static void Restart(bool autoRejoin = false, bool? debugger = null)
    {
        Start(autoRejoin, debugger ?? Debugger.IsAttached);
        Process.GetCurrentProcess().Kill();
    }

    private static void Start(bool autoRejoin, bool debugger)
    {
        // First "argument" is the invoked executable
        List<string> args = [.. Environment.GetCommandLineArgs().Skip(1)];

        args.Remove(ContinueArg);
        if (autoRejoin)
            args.Add(ContinueArg);

        args.Remove(DebugArg);
        if (debugger)
            args.Add(DebugArg);

        ProcessStartInfo currentStartInfo = Process.GetCurrentProcess().StartInfo;
        currentStartInfo.FileName = Application.ExecutablePath;
        currentStartInfo.Arguments = string.Join(" ", args.Select(a => $"\"{a}\""));

        Process.Start(currentStartInfo);
        Process.GetCurrentProcess().Kill();
    }

    /// <summary>
    /// This method attempts to disable JIT compiling for the assembly.
    /// This method will force any member access exceptions by methods to be thrown now instead of later.
    /// </summary>
    public static void Precompile(Assembly a)
    {
        Type[] types;
        try
        {
            types = a.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            StringBuilder sb = new();
            sb.AppendLine("LoaderExceptions: ");
            foreach (Exception e2 in e.LoaderExceptions)
                sb.Append(e2).AppendLine();
            LogFile.WriteLine(sb.ToString());
            throw;
        }

        foreach (Type t in types)
        {
            // Static constructors allow for early code execution which can cause issues later in the game
            if (HasStaticConstructor(t))
                continue;

            foreach (
                MethodInfo m in t.GetMethods(
                    BindingFlags.DeclaredOnly
                        | BindingFlags.NonPublic
                        | BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.Static
                )
            )
            {
                if (m.HasAttribute<HarmonyReversePatch>())
                    throw new Exception(
                        "Harmony attribute 'HarmonyReversePatch' found on the method '"
                            + m.Name
                            + "' is not compatible with Pulsar!"
                    );
                Precompile(m);
            }
        }
    }

    private static void Precompile(MethodInfo m)
    {
        if (!m.IsAbstract && !m.ContainsGenericParameters)
            RuntimeHelpers.PrepareMethod(m.MethodHandle);
    }

    private static bool HasStaticConstructor(Type t)
    {
        return t.GetConstructors(
                BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
            )
            .Any(c => c.IsStatic);
    }
}
