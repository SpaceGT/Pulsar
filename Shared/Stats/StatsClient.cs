using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

        return Post(ConsentUri, consentRequest) is not null;
    }

    // This function may be called from another thread.
    public static PluginStats DownloadStats()
    {
        if (!Steam.IsInitialized || !ConfigManager.Instance.Core.DataHandlingConsent)
        {
            LogFile.WriteLine("Downloading plugin statistics anonymously...");
            votingToken = null;
            return GetStats(StatsUri);
        }

        LogFile.WriteLine("Downloading plugin statistics, ratings and votes for " + PlayerHash);

        string url = $"{StatsUri}?playerHash={Uri.EscapeDataString(PlayerHash)}";
        var pluginStats = GetStats(url);

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

        return Post(TrackUri, trackRequest) is not null;
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

        string response = Post(VoteUri, voteRequest);
        return response is null ? null : JsonConvert.DeserializeObject<PluginStat>(response);
    }

    private static PluginStats GetStats(string url)
    {
        try
        {
            string response = NetworkClient
                .GetStringAsync(new Uri(url, UriKind.Absolute))
                .GetAwaiter()
                .GetResult();
            return JsonConvert.DeserializeObject<PluginStats>(response);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            LogFile.Error($"REST API request failed: GET {url} [{e.Message}]");
            return null;
        }
    }

    private static string Post(string url, object body)
    {
        try
        {
            using StringContent content = new(
                JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                "application/json"
            );
            return NetworkClient
                .PostStringAsync(new Uri(url, UriKind.Absolute), content)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            LogFile.Error($"REST API request failed: POST {url} [{e.Message}]");
            return null;
        }
    }
}
