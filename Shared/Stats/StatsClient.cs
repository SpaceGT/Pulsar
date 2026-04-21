using System.Collections.Generic;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Shared.Stats;

public static class StatsClient
{
    // API address
    public static string BaseUrl { get; set; }

    // API endpoints
    private static string ConsentUri => $"{BaseUrl}/Consent";
    private static string StatsUri => $"{BaseUrl}/Stats";
    private static string TrackUri => $"{BaseUrl}/Track";
    private static string VoteUri => $"{BaseUrl}/Vote";

    // Hashed Steam ID of the player
    private static string PlayerHash =>
        playerHash ??= Tools.GetStringHash($"{Steam.GetSteamId()}").Substring(0, 20);
    private static string playerHash;

    // Latest voting token received
    private static string votingToken;

    public static bool Consent(bool consent)
    {
        if (consent)
            LogFile.WriteLine($"Registering player consent on the statistics server");
        else
            LogFile.WriteLine(
                $"Withdrawing player consent, removing user data from the statistics server"
            );

        var consentRequest = new ConsentRequest() { PlayerHash = PlayerHash, Consent = consent };

        return SimpleHttpClient.Post(ConsentUri, consentRequest);
    }

    // This function may be called from another thread.
    public static PluginStats DownloadStats()
    {
        if (!ConfigManager.Instance.Core.DataHandlingConsent)
        {
            LogFile.WriteLine("Downloading plugin statistics anonymously...");
            votingToken = null;
            return SimpleHttpClient.Get<PluginStats>(StatsUri);
        }

        LogFile.WriteLine("Downloading plugin statistics, ratings and votes for " + PlayerHash);

        var parameters = new Dictionary<string, string> { ["playerHash"] = PlayerHash };
        var pluginStats = SimpleHttpClient.Get<PluginStats>(StatsUri, parameters);

        votingToken = pluginStats?.VotingToken;

        return pluginStats;
    }

    public static bool Track(string[] pluginIds)
    {
        var trackRequest = new TrackRequest
        {
            PlayerHash = PlayerHash,
            EnabledPluginIds = pluginIds,
        };

        return SimpleHttpClient.Post(TrackUri, trackRequest);
    }

    public static PluginStat Vote(string pluginId, int vote)
    {
        if (votingToken is null)
        {
            LogFile.Error($"Voting token is not available, cannot vote");
            return null;
        }

        LogFile.WriteLine($"Voting {vote} on plugin {pluginId}");
        var voteRequest = new VoteRequest
        {
            PlayerHash = PlayerHash,
            PluginId = pluginId,
            VotingToken = votingToken,
            Vote = vote,
        };

        var stat = SimpleHttpClient.Post<PluginStat, VoteRequest>(VoteUri, voteRequest);
        return stat;
    }
}
