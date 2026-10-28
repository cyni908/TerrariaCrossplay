using Newtonsoft.Json;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        [JsonProperty("support_journey_clients")]
        public bool SupportJourneyClients = false;

        [JsonProperty("debug_mode")]
        public bool DebugMode = false;

        /// <summary>
        /// Refuse to start when the server build is newer than the newest one this
        /// fork knows about. Off by default: the connect rewrite and item filter
        /// do not depend on the server build.
        /// </summary>
        [JsonProperty("strict_version_check")]
        public bool StrictVersionCheck = false;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
