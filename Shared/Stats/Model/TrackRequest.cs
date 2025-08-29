namespace Pulsar.Shared.Stats.Model;

// Request data sent to the StatsServer each time the game is started
public class TrackRequest
{
    // Hash of the player's Steam ID
    // Hexdump of the first 80 bits of SHA1($"{steamId}")
    // The client determines the ID of the player, never the server.
    // Using a hash is required for data protection and privacy.
    // Using a hash makes it impractical to track back usage or votes to
    // individual players, while still allowing for near-perfect deduplication.
    // It also prevents stealing all the Steam IDs from the server's database.
    public string PlayerHash { get; set; }

    // Ids of enabled plugins when the game started
    public string[] EnabledPluginIds { get; set; }
}
