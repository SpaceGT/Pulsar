using HarmonyLib;
using Keen.Game2;
using Keen.Game2.Client.UI.InGame;
using Keen.Game2.Client.UI.Library.Dialogs.LoadingDialog;
using Keen.Game2.Client.UI.Library.Dialogs.ThreeOptionsDialog;
using Keen.Game2.Game.SessionComponents;
using Keen.Game2.Simulation.Replication;
using Keen.Game2.Simulation.RuntimeSystems.Saves;
using Keen.VRage.Core;
using Keen.VRage.Core.Platform.CrashReporting;
using Keen.VRage.Library.Threading;
using Keen.VRage.Library.Utils;
using Pulsar.Modern.Screens;
using Pulsar.Shared;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace Pulsar.Modern.Loader;

internal static class LoaderTools
{
    private const string ContinueArg = "-continue";
    private const string DebugArg = "-debug";

    public static void AskToRestart()
    {
        bool isInGame = Singleton<VRageCore>.Instance.Engine.Get<GameAppComponent>().MainMenu is null;

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

        ScreenTools.GetSharedUIComponent().ShowDialog(new ThreeOptionsDialogViewModel(definition)
        {
            ConfirmAction = async () =>
            {
                var inGameUi = Singleton<VRageCore>.Instance.Engine.Get<GameAppComponent>().ClientSession.SessionComponents.Get<SessionInGameUISessionComponent>();

                await inGameUi.SaveAndExecute(afterMenu);
            },
            DefaultAction = () =>
            {
                afterMenu();
            },
        });
    }

    private static void Unload()
    {
        LogFile.Dispose();
        Singleton<VRageCore>.Instance.Exit();

        // Disable DiagnosticReporter so it does not throw an exception to report.
        // An exception being thrown would prevent the game from restarting.
        AccessTools.Field(typeof(DiagnosticReporter), "<Active>k__BackingField")
            .SetValue(Singleton<DiagnosticReporter>.Instance, false);

        Singleton<VRageCore>.Instance.Dispose();
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
