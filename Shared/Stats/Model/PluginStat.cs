namespace Pulsar.Shared.Stats.Model
{
    // Statistics for a single plugin
    public class PluginStat
    {
        // Number of players who successfully started SE with this plugin enabled anytime during the past 30 days
        public int Players { get; set; }

        // Total number of upvotes and downvotes since the beginning (votes do not expire)
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }

        // Whether the requesting player tried the plugin
        public bool Tried { get; set; }

        // Current vote of the requesting player
        // +1: Upvoted
        //  0: No vote (or cleared it)
        // -1: Downvoted
        public int Vote { get; set; }

        // Number of half stars [1-10] based on the upvote ratio, zero if there are not enough votes on the plugin yet
        public int Rating { get; set; }
    }
}
