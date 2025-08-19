using System;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;

namespace Pulsar.Shared
{
    public class Updater
    {
        // Stub file for a simple updater and loader for the Updater project
        // Launcher is responsible for the initial version checking

        public static void CheckUpdate(string repoName, bool outdated)
        {
            bool noUpdate = Tools.HasCommandArg("-noupdate");

            Assembly entryAssembly = Assembly.GetEntryAssembly();
            Version currentVersion = entryAssembly.GetName().Version;

            if (
                !noUpdate
                && GitHub.GetRepoVersion(repoName, out Version latestVersion)
                && currentVersion < latestVersion
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
                    GitHubPlugin.ClearGitHubCache();

                    // TODO: Update the updater project first
                    // Then let that project update everything
                    throw new NotImplementedException();
                }
                else if (result == DialogResult.Cancel)
                {
                    Environment.Exit(0);
                }
            }

            if (outdated)
            {
                StringBuilder message = new();
                message.Append("Space Engineers has been updated!\n");
                if (noUpdate)
                    message.Append("Please rebuild Pulsar for the current version.\n\n");
                else
                    message.Append("Please wait for Pulsar to update!\n\n");
                message.Append("Do you want to launch anyway? (expect instability)");

                DialogResult result = Tools.ShowMessageBox(
                    message.ToString(),
                    MessageBoxButtons.YesNo
                );

                if (result == DialogResult.No)
                    Environment.Exit(0);
            }
        }
    }
}
