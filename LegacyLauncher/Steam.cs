using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Pulsar.Shared;
using Steamworks;

namespace Pulsar.Legacy.Launcher
{
    public static class Steam
    {
        public const uint AppId = 244850u;
        private const int SteamTimeout = 30; // seconds

        public static void SubscribeToItem(ulong id) =>
            SteamUGC.SubscribeItem(new PublishedFileId_t(id));

        public static ulong GetSteamId() => SteamUser.GetSteamID().m_SteamID;

        public static void StartSteam()
        {
            if (!SteamAPI.IsSteamRunning())
            {
                try
                {
                    Process steam = Process.Start(
                        new ProcessStartInfo("cmd", "/c start steam://")
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                        }
                    );

                    if (steam != null)
                    {
                        for (int i = 0; i < SteamTimeout; i++)
                        {
                            Thread.Sleep(1000);

                            if (SteamAPI.Init())
                                return;
                        }
                    }
                }
                catch { }

                LogFile.WriteLine("Steam not detected!");
                Tools.ShowMessageBox("Steam must be running before you can start Space Engineers.");
                Environment.Exit(0);
            }

            SteamAPI.Init();
        }

        public static void EnsureAppID()
        {
            string exeLocation = Path.GetDirectoryName(
                Path.GetFullPath(Assembly.GetExecutingAssembly().Location)
            );
            string appIdFile = Path.Combine(exeLocation, "steam_appid.txt");

            if (!File.Exists(appIdFile))
            {
                LogFile.WriteLine(appIdFile + " does not exist, creating.");
                File.WriteAllText(appIdFile, AppId.ToString());
            }
        }
    }
}
