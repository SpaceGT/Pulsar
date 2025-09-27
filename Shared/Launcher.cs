using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Pulsar.Shared;

public class Launcher(string sePath)
{
    public static Mutex Mutex { get; private set; }

    public bool CanStart()
    {
        if (IsSpaceEngineersRunning())
        {
            Tools.ShowMessageBox("Error: Space Engineers is already running!");
            return false;
        }

        if (Environment.GetCommandLineArgs().Contains("-plugin"))
        {
            Tools.ShowMessageBox(
                "ERROR: \"-plugin\" support has been dropped!\n"
                    + "Use \"-sources\" add plugins there instead."
            );
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
        string callerName = Assembly.GetEntryAssembly().GetName().Name;
        Mutex = new Mutex(true, "Pulsar" + callerName, out bool isOwner);
        return !isOwner;
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
