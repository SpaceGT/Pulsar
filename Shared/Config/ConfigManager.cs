using Pulsar.Shared.Data;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Shared.Config
{
    public class ConfigManager
    {
        public const string HarmonyVersion = "2.3.6.0";

        public static ConfigManager Instance;

        public PluginList List { get; }
        public PluginConfig Config { get; }
        public SourcesConfig Sources { get; }
        public PluginStats Stats { get; private set; }
        public bool HasLocal { get; set; }
        public IDependency Dependencies { get; private set; }
        public bool DebugCompileAll { get; }
        public string PulsarDir { get; }

        public ConfigManager(
            string pulsarDir,
            IDependency dependencies,
            bool debugCompileAll = false
        )
        {
            Instance = this;

            Dependencies = dependencies;
            LogFile.Init(pulsarDir);

            PulsarDir = pulsarDir;
            DebugCompileAll = debugCompileAll;
            Config = PluginConfig.Load(pulsarDir);
            Config.CheckGameVersion();
            Sources = SourcesConfig.Load(pulsarDir);
            List = new PluginList(pulsarDir, Config, Sources);
            dependencies.UpdateWorkshopItems(List, Config);

            Config.Init(List, DebugCompileAll);
        }

        public void UpdatePlayerStats()
        {
            Stats = StatsClient.DownloadStats();
        }
    }
}
