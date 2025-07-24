using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Pulsar.Legacy.Launcher;

namespace Pulsar.Shared
{
    public class Launcher
    {
        private const int MutexTimeout = 1000; // ms

        private bool newMutex;
        private readonly Mutex mutex;
        private readonly string originalAssemblyFile;
        private readonly string sePath;

        public readonly LauncherConfig config;
        public readonly string Location;

        public Launcher(string programGuid, string originalAssemblyFile, string pulsarDir)
        {
            mutex = new Mutex(true, programGuid, out newMutex);
            Location = Path.GetDirectoryName(
                Path.GetFullPath(Assembly.GetCallingAssembly().Location)
            );
            sePath = Path.Combine(Location, originalAssemblyFile);
            config = LauncherConfig.Load(Path.Combine(pulsarDir, "launcher.xml"));
            this.originalAssemblyFile = originalAssemblyFile;
        }

        public bool CanStart()
        {
            if (!IsSingleInstance())
            {
                Tools.ShowMessageBox("Error: Space Engineers is already running!");
                return false;
            }

            if (!IsInGameFolder())
            {
                Tools.ShowMessageBox(
                    $"Error: {originalAssemblyFile} not found!\nIs {Path.GetFileName(Assembly.GetCallingAssembly().Location)} in the Bin64 folder?"
                );
                return false;
            }

            if (!VerifyFiles())
            {
                Tools.ShowMessageBox("ERROR: You have a broken Pulsar insallation!");
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

        private bool IsInGameFolder()
        {
            return File.Exists(sePath);
        }

        private bool IsSingleInstance()
        {
            // Check for other SpaceEngineersLauncher.exe
            if (!newMutex)
            {
                try
                {
                    newMutex = mutex.WaitOne(MutexTimeout);
                    if (!newMutex)
                        return false;
                }
                catch (AbandonedMutexException) { } // Abandoned probably means that the process was killed or crashed
            }

            // Check for other SpaceEngineers.exe
            if (
                Process
                    .GetProcessesByName(originalAssemblyFile.Replace("exe", ""))
                    .Any(x =>
                        x.MainModule.FileName.Equals(sePath, StringComparison.OrdinalIgnoreCase)
                    )
            )
                return false;

            return true;
        }

        private bool VerifyFiles()
        {
            if (config.Files != null)
            {
                foreach (string file in config.Files)
                {
                    if (!File.Exists(Path.Combine(Location, file)))
                    {
                        LogFile.WriteLine(
                            "WARNING: File verification failed, file does not exist: " + file
                        );
                        return false;
                    }
                }
            }

            return true;
        }

        public void ReleaseMutex()
        {
            if (newMutex)
                mutex.Close();
        }
    }
}
