using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using System.Text;
using VRage.Game;
using avaness.PluginLoader.Data;

namespace avaness.PluginLoader.Config
{
    public class PluginConfig
    {
        private const string fileName = "config.xml";

        private string filePath;
        private PluginList list;

        [XmlArray]
        [XmlArrayItem("Id")]
        public string[] Plugins
        {
            get { return enabledPlugins.Keys.ToArray(); }
            set
            {
                enabledPlugins.Clear();
                foreach (string id in value)
                    enabledPlugins[id] = null;
            }
        }
        public IEnumerable<PluginData> EnabledPlugins => enabledPlugins.Values;
        private readonly Dictionary<string, PluginData> enabledPlugins = new Dictionary<string, PluginData>();

        [XmlArray]
        [XmlArrayItem("Profile")]
        public Profile[] Profiles
        {
            get { return ProfileMap.Values.ToArray(); }
            set
            {
                ProfileMap.Clear();
                foreach (var profile in value.Where(x => x?.Key != null))
                    ProfileMap[profile.Key] = profile;
            }
        }

        [XmlIgnore]
        public readonly Dictionary<string, Profile> ProfileMap = new();

        [XmlArray]
        [XmlArrayItem("Config")]
        public PluginDataConfig[] PluginSettings
        {
            get { return pluginSettings.Values.ToArray(); }
            set
            {
                pluginSettings.Clear();
                foreach (PluginDataConfig config in value.Where(x => x?.Id != null))
                    pluginSettings[config.Id] = config;
            }
        }
        private readonly Dictionary<string, PluginDataConfig> pluginSettings = new Dictionary<string, PluginDataConfig>();

        public int GameVersion { get; set; }
        [XmlIgnore]
        public bool GameVersionChanged { get; private set; }

        // Base URL for the statistics server, change to http://localhost:5000 in config.xml for local development
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public string StatsServerBaseUrl { get; }

        // User consent to use the StatsServer
        public bool DataHandlingConsent { get; set; }
        public string DataHandlingConsentDate { get; set; }

        private int networkTimeout = 5000;
        public int NetworkTimeout
        {
            get
            {
                return networkTimeout;
            }
            set
            {
                if (value < 100)
                    networkTimeout = 100;
                else if (value > 60000)
                    networkTimeout = 60000;
                else
                    networkTimeout = value;
            }
        }

        public int Count => enabledPlugins.Count;

        public bool AllowIPv6 { get; set; } = true;

        public PluginConfig()
        {
        }

        public void Init(PluginList plugins, bool debugCompileAll)
        {
            list = plugins;

            bool save = false;
            StringBuilder sb = new StringBuilder("Enabled plugins: ");

            foreach(PluginData plugin in plugins)
            {
                string id = plugin.Id;
                bool enabled = IsEnabled(id);

                if (enabled || (debugCompileAll && !plugin.IsLocal && plugin.IsCompiled))
                {
                    sb.Append(id).Append(", ");
                    enabledPlugins[id] = plugin;
                }

                if (LoadPluginData(plugin))
                    save = true;
            }

            if (enabledPlugins.Count > 0)
                sb.Length -= 2;
            else
                sb.Append("None");
            LogFile.WriteLine(sb.ToString());

            foreach (KeyValuePair<string, PluginData> kv in enabledPlugins.Where(x => x.Value == null).ToArray())
            {
                LogFile.Warn($"{kv.Key} was in the config but is no longer available");
                enabledPlugins.Remove(kv.Key);
                save = true;
            }

            foreach (string id in pluginSettings.Keys.Where(x => !plugins.Contains(x)).ToArray())
            {
                LogFile.Warn($"{id} had settings in the config but is no longer available");
                pluginSettings.Remove(id);
                save = true;
            }

            if (save)
                Save();
        }

        public void CheckGameVersion()
        {
            int currentGameVersion = MyFinalBuildConstants.APP_VERSION?.Version ?? 0;
            int storedGameVersion = GameVersion;
            if (currentGameVersion != 0)
            {
                if (storedGameVersion == 0)
                {
                    GameVersion = currentGameVersion;
                    Save();
                }
                else if (storedGameVersion != currentGameVersion)
                {
                    GameVersion = currentGameVersion;
                    GameVersionChanged = true;
                    Save();
                }
            }
        }

        public void Disable()
        {
            enabledPlugins.Clear();
        }


        public void Save()
        {
            try
            {
                LogFile.WriteLine("Saving config");
                XmlSerializer serializer = new XmlSerializer(typeof(PluginConfig));
                if (File.Exists(filePath))
                    File.Delete(filePath);
                FileStream fs = File.OpenWrite(filePath);
                serializer.Serialize(fs, this);
                fs.Flush();
                fs.Close();
            }
            catch (Exception e)
            {
                LogFile.Error($"An error occurred while saving plugin config: " + e);
            }
        }

        public static PluginConfig Load(string mainDirectory)
        {
            string path = Path.Combine(mainDirectory, fileName);
            if (File.Exists(path))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PluginConfig));
                    PluginConfig config;
                    using (FileStream fs = File.OpenRead(path))
                        config = (PluginConfig)serializer.Deserialize(fs);
                    config.filePath = path;
                    return config;
                }
                catch (Exception e)
                {
                    LogFile.Error($"An error occurred while loading plugin config: " + e);
                }
            }

            return new PluginConfig
            {
                filePath = path
            };
        }

        public bool IsEnabled(string id)
        {
            return enabledPlugins.ContainsKey(id);
        }

        public void SetEnabled(string id, bool enabled)
        {
            SetEnabled(list[id], enabled);
        }

        public void SetEnabled(PluginData plugin, bool enabled)
        {
            string id = plugin.Id;
            if (IsEnabled(id) == enabled)
                return;

            if (enabled)
                Enable(plugin);
            else
                Disable(id);

            LoadPluginData(plugin); // Must be called because the enabled state has changed
        }

        private void Enable(PluginData plugin)
        {
            string id = plugin.Id;
            enabledPlugins[id] = plugin;
            list.SubscribeToItem(id);
        }

        private void Disable(string id)
        {
            enabledPlugins.Remove(id);
        }

        /// <summary>
        /// Loads the stored user data into the plugin. Returns true if the config was modified.
        /// </summary>
        public bool LoadPluginData(PluginData plugin)
        {
            PluginDataConfig settings;
            if (!pluginSettings.TryGetValue(plugin.Id, out settings))
                settings = null;
            if (plugin.LoadData(ref settings, IsEnabled(plugin.Id)))
            {
                if (settings == null)
                    pluginSettings.Remove(plugin.Id);
                else
                    pluginSettings[plugin.Id] = settings;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the stored user data for the plugin. Returns true if the config was modified.
        /// </summary>
        public bool RemovePluginData(string id)
        {
            return pluginSettings.Remove(id);
        }

        public void SavePluginData(GitHubPluginConfig settings)
        {
            pluginSettings[settings.Id] = settings;
        }
    }
}