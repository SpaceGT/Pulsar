using System;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Pulsar.Shared.Network;

namespace Pulsar.Shared
{
    public class Updater
    {
        // Stub file for a simple updater and loader for the Updater project
        // Launcher is responsible for the initial version checking

        public static void CheckUpdate(string repoName)
        {
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            Version currentVersion = entryAssembly.GetName().Version;

            if (
                GitHub.GetRepoVersion(repoName, out Version latestVersion)
                && currentVersion < latestVersion
                && !Tools.HasCommandArg("-noupdate")
            )
            {
                LogFile.WriteLine("An update is available to " + latestVersion);

                StringBuilder prompt = new();
                prompt.Append("An update is available for Pulsar:").AppendLine();
                prompt.Append(currentVersion).Append(" -> ").Append(latestVersion).AppendLine();
                prompt.Append("Would you like to update now?");

                DialogResult result = Tools.ShowMessageBox(
                    prompt.ToString(),
                    MessageBoxButtons.YesNoCancel
                );
                if (result == DialogResult.Yes)
                {
                    // TODO: Update the updater project first
                    // Then let that project update everything
                    Environment.Exit(0);
                }
                else if (result == DialogResult.Cancel)
                {
                    Environment.Exit(0);
                }
            }
        }
    }
}
