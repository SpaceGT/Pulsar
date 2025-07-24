using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Splash;
using Sandbox;
using VRage.Utils;

namespace Pulsar.Legacy.Launcher
{
    internal class Dependency : IDependency
    {
        public Version GetGameVersion() => Game.GetGameVersion();

        public ulong GetSteamID() => Steam.GetSteamId();

        public void InvokeOnGameThread(Action action) =>
            MySandboxGame.Static.Invoke(action, "Pulsar");

        public bool HasGameLog()
        {
            string file = MyLog.Default?.GetFilePath();
            return File.Exists(file) && file.EndsWith(".log");
        }

        public void ViewGameLog()
        {
            MyLog.Default.Flush();
            string file = MyLog.Default?.GetFilePath();
            if (File.Exists(file) && file.EndsWith(".log"))
                Process.Start(file);
            else
                Tools.ShowMessageBox(
                    "Game log not created yet!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
        }

        public void WriteGameLog(string line) => MyLog.Default?.WriteLine(line);

        // Note that mod plugins were removed from the workshop
        // The functions are here for legacy reasons
        public void SubscribeToItem(ulong id) => Steam.SubscribeToItem(id);

        public void UpdateWorkshopItems(PluginList pluginList, PluginConfig config)
        {
            SplashManager.Instance?.SetText($"Updating workshop items...");
            Steam.Update(
                pluginList
                    .GetSteamPlugins()
                    .Where(x => config.IsEnabled(x.Id))
                    .Select(x => x.WorkshopId)
            );
        }
    }
}
