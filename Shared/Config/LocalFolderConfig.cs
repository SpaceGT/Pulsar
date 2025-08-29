namespace Pulsar.Shared.Config;

public class LocalFolderConfig : PluginDataConfig
{
    public string DataFile { get; set; }
    public bool DebugBuild { get; set; } = true;
}
