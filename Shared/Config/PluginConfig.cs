using System;
using System.IO;
using System.Xml.Serialization;

namespace Pulsar.Shared.Config
{
    public class PluginConfig
    {
        private const string fileName = "config.xml";

        private string filePath;

        // Base URL for the statistics server, change to http://localhost:5000 in config.xml for local development
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public string StatsServerBaseUrl { get; }

        // User consent to use the StatsServer
        public bool DataHandlingConsent { get; set; }
        public string DataHandlingConsentDate { get; set; }

        private int networkTimeout = 5000;
        public int NetworkTimeout
        {
            get { return networkTimeout; }
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

        public bool AllowIPv6 { get; set; } = true;

        public PluginConfig() { }

        public void Save()
        {
            try
            {
                LogFile.WriteLine("Saving config");
                XmlSerializer serializer = new(typeof(PluginConfig));
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
                    XmlSerializer serializer = new(typeof(PluginConfig));
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

            return new PluginConfig { filePath = path };
        }
    }
}
