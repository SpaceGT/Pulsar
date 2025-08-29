using System.Xml.Serialization;

namespace Pulsar.Shared.Config;

[XmlInclude(typeof(LocalFolderConfig))]
[XmlInclude(typeof(GitHubPluginConfig))]
public abstract class PluginDataConfig
{
    public string Id { get; set; }
}
