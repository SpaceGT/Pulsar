using System;

namespace Pulsar.Shared.Config;

public class RemotePluginConfig
{
    public string Name { get; set; }
    public string Repo { get; set; }
    public string Branch { get; set; }
    public string File { get; set; }
    public DateTime? LastCheck { get; set; }

    /// <summary>
    /// Game version Pulsar last reached the main menu with, null if never
    /// </summary>
    public string GameVersion { get; set; }
    public bool Enabled { get; set; }
    public bool Trusted { get; set; }
}
