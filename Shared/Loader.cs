using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pulsar.Compiler;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;
using Pulsar.Shared.Splash;
using Pulsar.Shared.Stats;

namespace Pulsar.Shared
{
    public class Loader
    {
        public static bool DebugCompileAll = false;
        public static Loader Instance;
        public readonly List<(PluginData, Assembly)> Plugins = [];

        private readonly SplashManager splash;
        private readonly PluginConfig config;
        private readonly StringBuilder debugCompileResults = new();

        public Loader()
        {
            config = ConfigManager.Instance.Config;
            splash = SplashManager.Instance;

            GitHub.Init();

            splash?.SetText("Finding references...");
            List<string> assemblies = ReferenceHelper.GetAssemblies(AppDomain.CurrentDomain);
            DomainHelper.CreateAppDomain(ConfigManager.Instance.PulsarDir, assemblies);

            splash?.SetText("Starting...");

            if (config.GameVersionChanged)
                GitHubPlugin.ClearGitHubCache();

            StatsClient.OverrideBaseUrl(config.StatsServerBaseUrl);
            ConfigManager.Instance.UpdatePlayerStats();

            // Check harmony version
            Version expectedHarmony = new(ConfigManager.HarmonyVersion);
            Version actualHarmony = typeof(Harmony).Assembly.GetName().Version;
            if (expectedHarmony != actualHarmony)
                LogFile.Warn(
                    $"Unexpected Harmony version, plugins may be unstable. Expected {expectedHarmony} but found {actualHarmony}"
                );

            splash?.SetText("Instantiating plugins...");
            LogFile.WriteLine("Instantiating plugins");

            if (DebugCompileAll)
                debugCompileResults.Append("Plugins that failed to compile:").AppendLine();

            //TODO: Compile in parallel
            foreach (PluginData data in config.EnabledPlugins)
            {
                if (TryGetAssembly(data, out Assembly plugin))
                {
                    Plugins.Add((data, plugin));
                    if (data.IsLocal)
                        ConfigManager.Instance.HasLocal = true;
                }
                else if (DebugCompileAll)
                {
                    debugCompileResults
                        .Append(data.FriendlyName ?? "(null)")
                        .Append(" - ")
                        .Append(data.Id ?? "(null)")
                        .Append(" by ")
                        .Append(data.Author ?? "(null)")
                        .AppendLine();
                }
            }

            // FIXME: It can potentially run in the background speeding up the game's startup
            ReportEnabledPlugins();
        }

        private void ReportEnabledPlugins()
        {
            if (!ConfigManager.Instance.Config.DataHandlingConsent)
                return;

            splash?.SetText("Reporting plugin usage...");
            LogFile.WriteLine("Reporting plugin usage");

            // Skip local plugins, keep only enabled ones
            string[] trackablePluginIds =
            [
                .. config.EnabledPlugins.Where(x => !x.IsLocal).Select(x => x.Id),
            ];

            // Config has already been validated at this point so all enabled plugins will have list items
            // FIXME: Move into a background thread
            if (StatsClient.Track(trackablePluginIds))
                LogFile.WriteLine("List of enabled plugins has been sent to the statistics server");
            else
                LogFile.Error(
                    "Failed to send the list of enabled plugins to the statistics server"
                );
        }

        public static bool TryGetAssembly(PluginData data, out Assembly assembly)
        {
            assembly = null;

            if (data.Status == PluginStatus.Error || !data.TryLoadAssembly(out assembly))
                return false;

            return true;
        }
    }
}
