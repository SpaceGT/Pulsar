using System.Collections.Generic;

#pragma warning disable IDE1006 // Naming Styles
namespace Pulsar.Shared.Vdf.Steam;

public class LibraryFolder
{
    public string path { get; set; }
    public string label { get; set; }
    public ulong contentid { get; set; }
    public ulong totalsize { get; set; }
    public ulong update_clean_bytes_tally { get; set; }
    public ulong time_last_update_verified { get; set; }
    public Dictionary<ulong, ulong> apps { get; set; }
}
