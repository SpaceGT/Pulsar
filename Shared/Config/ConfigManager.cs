using System;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Shared.Config
{
    public interface IDependency
    {
        void OnMainThread(Action action);
        void SteamSubscribe(ulong id);
    }

    public class ConfigManager
    {
        public const string HarmonyVersion = "2.3.6.0";

        public static ConfigManager Instance;

        public PluginList List { get; }
        public PluginConfig Config { get; }
        public SourcesConfig Sources { get; }
        public PluginStats Stats { get; private set; }
        public bool SafeMode { get; set; }
        public bool HasLocal { get; set; }
        public IDependency Dependencies { get; private set; }
        public bool DebugCompileAll { get; }
        public string PulsarDir { get; }
        public string GameDir { get; }
        public string ModDir { get; }
        public ulong SteamId { get; }

        public ConfigManager(
            string pulsarDir,
            string gameDir,
            string modDir,
            ulong steamId,
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
            SteamId = steamId;
            DebugCompileAll = debugCompileAll;

            Config = PluginConfig.Load(pulsarDir);
            Config.CheckGameVersion(gameVersion);
            Sources = SourcesConfig.Load(pulsarDir);
            List = new PluginList(pulsarDir, Config, Sources);

            Config.Init(List, DebugCompileAll);
        }

        public void UpdatePlayerStats()
        {
            Stats = StatsClient.DownloadStats();
        }
    }
}
