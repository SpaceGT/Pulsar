using System;
using System.IO;
using System.Xml.Serialization;
using Pulsar.Shared;

namespace Pulsar.Legacy.Launcher
{
    [XmlRoot("LauncherConfig")]
    public class LauncherConfig
    {
        private string filePath;

        public string LoaderVersion { get; set; }

        public bool NoUpdates { get; set; }

        [XmlArrayItem("File")]
        public string[] Files { get; set; }

        private int networkTimeout = 10000;
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

        public LauncherConfig() { }

        public static LauncherConfig Load(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    XmlSerializer serializer = new(typeof(LauncherConfig));
                    FileStream fs = File.OpenRead(filePath);
                    LauncherConfig config = (LauncherConfig)serializer.Deserialize(fs);
                    fs.Close();
                    config.filePath = filePath;
                    return config;
                }
                catch (Exception e)
                {
                    LogFile.WriteLine($"An error occurred while loading launcher config: " + e);
                }
            }

            return new LauncherConfig { filePath = filePath };
        }

        public void Save()
        {
            try
            {
                LogFile.WriteLine("Saving config");
                XmlSerializer serializer = new(typeof(LauncherConfig));
                if (File.Exists(filePath))
                    File.Delete(filePath);
                FileStream fs = File.OpenWrite(filePath);
                serializer.Serialize(fs, this);
                fs.Flush();
                fs.Close();
            }
            catch (Exception e)
            {
                LogFile.WriteLine($"An error occurred while saving launcher config: " + e);
            }
        }
    }
}
