// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Pulsar.Shared.Stats.Model
{
    // Request data received from Pulsar to store user consent or withdrawal,
    // this request is NOT sent if the user does not give consent in the first place
    public class ConsentRequest
    {
        // Hash of the player's Steam ID
        public string PlayerHash { get; set; }

        // True if the consent has just given, false if has just withdrawn
        public bool Consent { get; set; }
    }
}
