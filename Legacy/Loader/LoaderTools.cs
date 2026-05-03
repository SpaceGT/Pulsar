using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Pulsar.Shared;
using Sandbox;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Audio;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Loader;

public static class LoaderTools
{
    private const string ContinueArg = "-continue";
    private const string DebugArg = "-debug";

    public static void AskToRestart()
    {
        bool isInGame = MySession.Static is not null;

        void RestartGame()
        {
            Unload();
            Restart(isInGame);
        }

        if (isInGame)
            AskSave(RestartGame);
        else
            RestartGame();
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

    private static void Unload()
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
        Shared.Launcher.Mutex.Close();
        ExecSelf(autoRejoin, debugger ?? Debugger.IsAttached);
        // execv only returns on failure; if we get here, fall back to killing the process
        Process.GetCurrentProcess().Kill();
    }

    /// <summary>
    /// Replaces the current process image with a fresh invocation of the same executable
    /// using the Linux execv(2) syscall. On success this call does not return.
    /// </summary>
    private static void ExecSelf(bool autoRejoin, bool debugger)
    {
        string executable =
            Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Cannot determine current executable path for restart"
            );

        // First "argument" is the invoked executable
        List<string> args = [.. Environment.GetCommandLineArgs().Skip(1)];

        args.Remove(ContinueArg);
        if (autoRejoin)
            args.Add(ContinueArg);

        args.Remove(DebugArg);
        if (debugger)
            args.Add(DebugArg);

        // execv expects argv[0] to be the program name (conventionally the executable path)
        // and the array to be terminated with a null pointer.
        string[] argv = new string[args.Count + 2];
        argv[0] = executable;
        for (int i = 0; i < args.Count; i++)
            argv[i + 1] = args[i];
        argv[args.Count + 1] = null;

        execv(executable, argv);

        // Only reached on failure
        int errno = Marshal.GetLastWin32Error();
        LogFile.WriteLine($"execv failed for {executable} (errno={errno})");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int execv(string path, string[] argv);

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
