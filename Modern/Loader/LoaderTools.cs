using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using HarmonyLib;
using Keen.Game2;
using Keen.Game2.Client.UI.InGame;
using Keen.Game2.Client.UI.Library.Dialogs.ThreeOptionsDialog;
using Keen.VRage.Core;
using Keen.VRage.Core.Platform.CrashReporting;
using Keen.VRage.Library.Utils;
using Pulsar.Modern.Screens;
using Pulsar.Shared;

namespace Pulsar.Modern.Loader;

internal static class LoaderTools
{
    // This is changed to -startLast from -continue, as SE2 already has a system to load the last world you were in at startup.
    private const string ContinueArg = "-startLast";
    private const string DebugArg = "-debug";

    public static void AskToRestart()
    {
        bool isInGame =
            Singleton<VRageCore>.Instance.Engine.Get<GameAppComponent>().MainMenu is null;

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

    private static void AskSave(Action afterMenu)
    {
        var definition = ScreenTools.GetDefaultYesNoCancelDialog();
        definition.Title = ScreenTools.GetKeyFromString("Please Confirm");
        definition.Content = ScreenTools.GetKeyFromString("Save changes before restarting game?");
        definition.CancelOption = ScreenTools.GetKeyFromString("Don't Restart");

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new ThreeOptionsDialogViewModel(definition)
                {
                    ConfirmAction = async () =>
                    {
                        var inGameUi = Singleton<VRageCore>
                            .Instance.Engine.Get<GameAppComponent>()
                            .ClientSession.SessionComponents.Get<SessionInGameUISessionComponent>();

                        await inGameUi.SaveAndExecute(afterMenu);
                    },
                    DefaultAction = () =>
                    {
                        afterMenu();
                    },
                }
            );
    }

    private static void Unload()
    {
        LogFile.Dispose();
    }

    public static void Restart(bool autoRejoin = false, bool? debugger = null)
    {
        Shared.Launcher.Mutex.Close();
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

        ProcessStartInfo startInfo = new(
            fileName: Application.ExecutablePath,
            arguments: string.Join(" ", args.Select(a => $"\"{a}\""))
        );

        Process.Start(startInfo);
    }
}
