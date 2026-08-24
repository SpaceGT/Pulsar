using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Pulsar.Protocol.Interface;
using Pulsar.Shared.Arguments;

namespace Pulsar.Shared;

public class Launcher(string sePath)
{
    private static Mutex mutex;
    private static bool ownsMutex;

    public bool CanStart()
    {
        if (!Flags.Current.MultiInstance && IsSpaceEngineersRunning())
        {
            string message = "Space Engineers is already running!";
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            return false;
        }

        if (Environment.GetCommandLineArgs().Contains("-plugin"))
        {
            string message =
                "\"-plugin\" support has been dropped!\n"
                + "Use \"-sources\" add plugins there instead.";
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            return false;
        }

        return true;
    }

    private bool IsSpaceEngineersRunning()
    {
        string seName = Path.GetFileNameWithoutExtension(sePath);
        return Process
            .GetProcessesByName(seName)
            .Select(process => process.MainModule.FileName)
            .Any(path => path.Equals(sePath, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsOtherPulsarRunning()
    {
        if (Flags.Current.MultiInstance)
            return false;

        string callerName = Assembly.GetEntryAssembly().GetName().Name;
        string mutexName = callerName == "Modern" ? "Modern" : "Legacy";

#if NETFRAMEWORK
        mutex = new Mutex(false, $"Pulsar.{mutexName}");
#else
        NamedWaitHandleOptions options = new()
        {
            CurrentUserOnly = true,
            CurrentSessionOnly = false,
        };
        mutex = new Mutex(false, $"Pulsar.{mutexName}", options);
#endif

        try
        {
            ownsMutex = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        return !ownsMutex;
    }

    public static void ReleaseInstanceLock()
    {
        if (ownsMutex)
            mutex.ReleaseMutex();

        mutex?.Dispose();
        mutex = null;
        ownsMutex = false;
    }

    public bool VerifyConfig()
    {
        string seFolder = Path.GetDirectoryName(sePath);
        bool hasConfig = Tools.GetFiles(seFolder, ["*.config"], []).Any();
        string configPath = Assembly.GetEntryAssembly().Location + ".config";

        if (hasConfig && !File.Exists(configPath))
            return false;

        return true;
    }
}
