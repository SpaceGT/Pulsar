using System;
using System.Threading.Tasks;
using Pulsar.Shared.Stats;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Shared.Config;

public class ConfigManager
{
    public const string HarmonyVersion = "2.3.6.0";

    public static ConfigManager Instance { get; private set; }

    public PluginList List { get; private set; }
    public CoreConfig Core { get; private set; }
    public SourcesConfig Sources { get; private set; }
    public ProfilesConfig Profiles { get; private set; }
    public PluginStats Stats { get; private set; }
    public Version GameVersion { get; private set; }

    public bool DebugCompileAll { get; private set; }
    public string PulsarDir { get; private set; }
    public string GameDir { get; private set; }
    public string ModDir { get; private set; }

    public bool SafeMode { get; set; }
    public bool HasLocal { get; set; }

    public static void EarlyInit(string pulsarDir, bool debugCompileAll)
    {
        Instance = new()
        {
            SafeMode = false,
            PulsarDir = pulsarDir,
            DebugCompileAll = debugCompileAll,
            Core = CoreConfig.Load(pulsarDir),
        };
    }

    public static void Init(string gameDir, string modDir, Version gameVersion)
    {
        ConfigManager i = Instance;
        i.GameDir = gameDir;
        i.ModDir = modDir;
        i.GameVersion = gameVersion;
        i.Profiles = ProfilesConfig.Load(i.PulsarDir);
        i.Sources = SourcesConfig.Load(i.PulsarDir);
        i.List = new PluginList(i.PulsarDir, i.Sources, i.Profiles);
    }

    public void UpdatePlayerStats()
    {
        Task.Run(() =>
        {
            Stats = StatsClient.DownloadStats();
        });
    }
}
