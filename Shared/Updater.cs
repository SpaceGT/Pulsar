using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Pulsar.Legacy.Launcher;
using Pulsar.Shared.Network;

namespace Pulsar.Shared
{
    internal class Updater
    {
        // Stub file for a simple updater and loader for the Updater project
        // Launcher is responsible for the initial version checking
        private static readonly Regex VersionRegex = new(@"^v(\d+\.)*\d+$");

        public static void Update(LauncherConfig config, string repoName)
        {
            string currentVersion = null;
            if (
                !string.IsNullOrWhiteSpace(config.LoaderVersion)
                && VersionRegex.IsMatch(config.LoaderVersion)
            )
            {
                currentVersion = config.LoaderVersion;
                LogFile.WriteLine("Pulsar " + currentVersion);
            }
            else
            {
                LogFile.WriteLine("Pulsar version unknown");
            }

            if (
                GitHub.GetRepoVersion(repoName, out string latestVersion)
                && currentVersion != latestVersion
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
                }
                else if (result == DialogResult.Cancel)
                {
                    Environment.Exit(0);
                }
            }
        }
    }
}
