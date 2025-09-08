using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Shared;

public class Launcher(string sePath, string dependencyDir, string checksum)
{
    public static Mutex Mutex { get; private set; }

    public bool CanStart()
    {
        if (IsSpaceEngineersRunning())
        {
            Tools.ShowMessageBox("Error: Space Engineers is already running!");
            return false;
        }

        if (Tools.HasCommandArg("-plugin"))
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

    public bool Verify(bool noUpdates = false)
    {
        if (VerifyFiles())
            return true;

        MessageBoxButtons buttons;
        string message = "You have a broken Pulsar insallation!\n";

        if (noUpdates)
        {
            message += "Please rebuild or manually redownload.";
            buttons = MessageBoxButtons.OK;
        }
        else
        {
            message += "Attempt to download the latest version?";
            buttons = MessageBoxButtons.YesNo;
        }

        DialogResult result = Tools.ShowMessageBox(message, buttons);

        if (result != DialogResult.Yes)
            Environment.Exit(1);

        return false;
    }

    private bool VerifyFiles()
    {
        if (!Directory.Exists(dependencyDir))
            return false;

        if (checksum is not null && Tools.GetFolderHash(dependencyDir) != checksum)
            return false;

        string seFolder = Path.GetDirectoryName(sePath);
        bool hasConfig = Tools.GetFiles(seFolder, ["*.config"], []).Any();
        string configPath = Assembly.GetEntryAssembly().Location + ".config";

        if (hasConfig && !File.Exists(configPath))
            return false;

        return true;
    }
}
