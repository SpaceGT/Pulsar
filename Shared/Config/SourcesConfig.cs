using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Pulsar.Shared.Config
{
    public class SourcesConfig
    {
        private const string fileName = "sources.xml";
        private string filePath;

        public bool ShowWarning { get; set; } = true;
        public int MaxSourceAge { get; set; } = 24;

        [XmlArray]
        [XmlArrayItem("LocalHub")]
        public LocalHubConfig[] LocalHubSources
        {
            get { return [.. localHubSources]; }
            set
            {
                localHubSources.Clear();
                foreach (LocalHubConfig url in value)
                    localHubSources.Add(url);
            }
        }
        private readonly HashSet<LocalHubConfig> localHubSources = [];

        [XmlArray]
        [XmlArrayItem("RemoteHub")]
        public RemoteHubConfig[] RemoteHubSources
        {
            get { return [.. remoteHubSources]; }
            set
            {
                remoteHubSources.Clear();
                foreach (RemoteHubConfig url in value)
                    remoteHubSources.Add(url);
            }
        }
        private readonly HashSet<RemoteHubConfig> remoteHubSources = [];

        [XmlArray]
        [XmlArrayItem("RemotePlugin")]
        public RemotePluginConfig[] RemotePluginSources
        {
            get { return [.. remotePluginSources]; }
            set
            {
                remotePluginSources.Clear();
                foreach (RemotePluginConfig url in value)
                    remotePluginSources.Add(url);
            }
        }
        private readonly HashSet<RemotePluginConfig> remotePluginSources = [];

        [XmlArray]
        [XmlArrayItem("LocalPlugin")]
        public LocalPluginConfig[] LocalPluginSources
        {
            get { return [.. localPluginSources]; }
            set
            {
                localPluginSources.Clear();
                foreach (LocalPluginConfig url in value)
                    localPluginSources.Add(url);
            }
        }
        private readonly HashSet<LocalPluginConfig> localPluginSources = [];

        [XmlArray]
        [XmlArrayItem("Mod")]
        public ModConfig[] ModSources
        {
            get { return [.. modSources]; }
            set
            {
                modSources.Clear();
                foreach (ModConfig url in value)
                    modSources.Add(url);
            }
        }
        private readonly HashSet<ModConfig> modSources = [];

        public SourcesConfig() { }

        public void Save()
        {
            try
            {
                LogFile.WriteLine("Saving config");
                XmlSerializer serializer = new(typeof(SourcesConfig));
                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                FileStream fs = File.OpenWrite(filePath);
                serializer.Serialize(fs, this);
                fs.Flush();
                fs.Close();
            }
            catch (Exception e)
            {
                LogFile.Error($"An error occurred while saving sources config: " + e);
            }
        }

        public static SourcesConfig Load(string mainDirectory)
        {
            SourcesConfig config;
            string path = Path.Combine(mainDirectory, "Sources", fileName);
            if (File.Exists(path))
            {
                try
                {
                    XmlSerializer serializer = new(typeof(SourcesConfig));
                    using (FileStream fs = File.OpenRead(path))
                        config = (SourcesConfig)serializer.Deserialize(fs);
                    config.filePath = path;
                    return config;
                }
                catch (Exception e)
                {
                    LogFile.Error($"An error occurred while loading sources config: " + e);
                }
            }

            config = new SourcesConfig
            {
                filePath = path,
                RemoteHubSources =
                [
                    new RemoteHubConfig()
                    {
                        Name = "PluginHub",
                        Repo = "StarCpt/PluginHub",
                        Branch = "main",
                        Enabled = true,
                        Hash = null,
                        LastCheck = null,
                        Trusted = true,
                    },
                ],
            };

            return config;
        }
    }
}
