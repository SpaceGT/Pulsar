using System;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace Pulsar.Shared.Config;

public class PluginConfig
{
    private const string fileName = "config.xml";
    private string filePath;

    public string StatsServerBaseUrl { get; }
    public bool DataHandlingConsent { get; set; }
    public string DataHandlingConsentDate { get; set; }
    public bool AllowIPv6 { get; set; } = true;
    public int NetworkTimeout { get; set; } = 5000;

    [XmlIgnore]
    public Version GameVersion { get; set; }

    [XmlElement("GameVersion")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string GameVersionString
    {
        get => GameVersion?.ToString();
        set => GameVersion = string.IsNullOrWhiteSpace(value) ? null : new Version(value);
    }

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
