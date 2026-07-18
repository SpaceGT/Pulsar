using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Pulsar.Interface.Protocol;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;
using Pulsar.Shared.Splash;
using Pulsar.Shared.Stats;

namespace Pulsar.Shared;

public class Loader
{
    public static Loader Instance;
    public readonly List<(PluginData, Assembly)> Plugins = [];

    private readonly CoreConfig config;
    private readonly SplashManager splash;
    private readonly ProfilesConfig profiles;

    public Loader(string statsServer, string[] forceEnable = null)
    {
        ConfigManager manager = ConfigManager.Instance;
        config = manager.Core;
        profiles = manager.Profiles;

        splash = SplashManager.Instance;

        if (Tools.IsEscapePressed())
        {
            PromptResult result = Tools.ShowMessageBox(
                "Escape pressed. Start the game with all plugins disabled?",
                PromptButtons.YesNo,
                PromptIcon.Question
            );

            if (result == PromptResult.Yes)
            {
                LogFile.Warn("Safe mode active. No plugins will be loaded!");
                ConfigManager.Instance.SafeMode = true;
            }
        }

        GitHub.Init();
        LogEnabledPlugins();

        StatsClient.BaseUrl = config.StatsServerBaseUrl ?? statsServer;
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

        StringBuilder debugCompileResults = new();
        if (Flags.CheckAllPlugins)
            debugCompileResults.Append("Plugins that failed to compile:").AppendLine();

        // FIXME: Treat as a plugin dependency in the future.
        foreach (string id in forceEnable ?? [])
        {
            if (
                ConfigManager.Instance.List.TryGetPlugin(id, out PluginData data)
                && data.TryLoadAssembly(out Assembly plugin)
            )
            {
                Plugins.Add((data, plugin));
                continue;
            }

            string message = $"Failed to load core plugin '{id}'";
            LogFile.Error(message);

            string fullMessage = $"{message}\nPulsar cannot continue loading!";
            Tools.ShowMessageBox(fullMessage, PromptButtons.Ok, PromptIcon.Error);

            Environment.Exit(1);
        }

        //TODO: Compile in parallel
        foreach (PluginData data in GetEnabledPlugins())
        {
            if (forceEnable.Contains(data.Id))
                continue;

            if (data.TryLoadAssembly(out Assembly plugin))
            {
                Plugins.Add((data, plugin));
                if (data.IsLocal)
                    ConfigManager.Instance.HasLocal = true;
            }
            else if (Flags.CheckAllPlugins && data is not ModPlugin && data.IsSupportedRuntime())
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

        if (Flags.CheckAllPlugins)
            LogFile.WriteLine(debugCompileResults.ToString());

        Task.Run(ReportEnabledPlugins);
    }

    private void ReportEnabledPlugins()
    {
        if (!ConfigManager.Instance.Core.DataHandlingConsent)
            return;

        splash?.SetText("Reporting plugin usage...");
        LogFile.WriteLine("Reporting plugin usage");

        // Skip local plugins, keep only enabled ones
        string[] trackablePluginIds = [.. profiles.Current.GetPluginIDs(false)];

        // Config has already been validated at this point so all enabled plugins will have list items
        // FIXME: Move into a background thread
        if (StatsClient.Track(trackablePluginIds))
            LogFile.WriteLine("List of enabled plugins has been sent to the statistics server");
        else
            LogFile.Error("Failed to send the list of enabled plugins to the statistics server");
    }

    private IEnumerable<PluginData> GetEnabledPlugins()
    {
        foreach (PluginData plugin in ConfigManager.Instance.List)
        {
            string id = plugin.Id;
            bool enabled = profiles.Current.Contains(id);

            if (enabled || (Flags.CheckAllPlugins && !plugin.IsLocal && plugin.IsCompiled))
                yield return plugin;
        }
    }

    private void LogEnabledPlugins()
    {
        StringBuilder sb = new("Enabled plugins: ");
        string[] plugins = [.. GetEnabledPlugins().Select(x => x.Id)];

        if (plugins.Length > 0)
            sb.Append(string.Join(", ", plugins));
        else
            sb.Append("None");

        LogFile.WriteLine(sb.ToString());
    }
}
