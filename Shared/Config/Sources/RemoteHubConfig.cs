using System;

namespace Pulsar.Shared.Config;

public class RemoteHubConfig
{
    public string Name { get; set; }
    public string Repo { get; set; }
    public string Branch { get; set; }
    public DateTime? LastCheck { get; set; }
    public string Hash { get; set; }
    public bool Enabled { get; set; }
    public bool Trusted { get; set; }
}
