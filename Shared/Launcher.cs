using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Pulsar.Shared
{
    public class Launcher
    {
        private const int MutexTimeout = 1000; // ms

        private bool newMutex;
        private readonly Mutex mutex;
        private readonly string sePath;
        private readonly string dependencyDir;
        private readonly string checksum;

        public readonly string Location;

        public Launcher(string sePath, string dependencyDir, string checksum)
        {
            string programGuid = GetCallerGuid();

            this.sePath = sePath;
            this.checksum = checksum;
            this.dependencyDir = dependencyDir;

            mutex = new Mutex(true, programGuid, out newMutex);
            Location = Path.GetDirectoryName(
                Path.GetFullPath(Assembly.GetCallingAssembly().Location)
            );
        }

        public bool CanStart()
        {
            if (!IsSingleInstance())
            {
                Tools.ShowMessageBox("Error: Space Engineers is already running!");
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

        private bool IsSingleInstance()
        {
            // Check for other Pulsar instances
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

            // Check for other Space Engineers instances
            if (
                Process
                    .GetProcessesByName(Path.GetFileNameWithoutExtension(sePath))
                    .Any(x =>
                        x.MainModule.FileName.Equals(sePath, StringComparison.OrdinalIgnoreCase)
                    )
            )
                return false;

            return true;
        }

        private bool VerifyFiles()
        {
            string configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (!File.Exists(configPath) || !Directory.Exists(dependencyDir))
                return false;

            if (checksum is not null && Tools.GetFolderHash(dependencyDir) != checksum)
                return false;

            return true;
        }

        public void ReleaseMutex()
        {
            if (newMutex)
                mutex.Close();
        }

        private static string GetCallerGuid()
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            GuidAttribute attribute = assembly
                .GetCustomAttributes<GuidAttribute>()
                .FirstOrDefault();

            return attribute?.Value ?? null;
        }
    }
}
