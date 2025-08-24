using System;
using System.Threading.Tasks;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Shared.Config
{
    public interface IDependency
    {
        void OnMainThread(Action action);
    }

    public class ConfigManager
    {
        public const string HarmonyVersion = "2.3.6.0";

        public static ConfigManager Instance;

        public PluginList List { get; }
        public PluginConfig Config { get; }
        public SourcesConfig Sources { get; }
        public ProfilesConfig Profiles { get; }
        public PluginStats Stats { get; private set; }
        public Version GameVersion { get; }
        public bool SafeMode { get; set; }
        public bool HasLocal { get; set; }
        public IDependency Dependencies { get; private set; }
        public bool DebugCompileAll { get; }
        public string PulsarDir { get; }
        public string GameDir { get; }
        public string ModDir { get; }

        public ConfigManager(
            string pulsarDir,
            string gameDir,
            string modDir,
            Version gameVersion,
            IDependency dependencies,
            bool debugCompileAll = false
        )
        {
            Instance = this;
            SafeMode = false;
            Dependencies = dependencies;
            PulsarDir = pulsarDir;
            GameDir = gameDir;
            ModDir = modDir;
            DebugCompileAll = debugCompileAll;
            GameVersion = gameVersion;

            Config = PluginConfig.Load(pulsarDir);
            Sources = SourcesConfig.Load(pulsarDir);
            Profiles = ProfilesConfig.Load(pulsarDir);
            List = new PluginList(pulsarDir, Config, Sources, Profiles);
        }

        public void UpdatePlayerStats()
        {
            Task.Run(() =>
            {
                Stats = StatsClient.DownloadStats();
            });
        }
    }
}
