using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Attribution.Dto
{
    /// <summary>One id a third-party service knows the player's account by.</summary>
    [Serializable]
    public sealed class ExternalIdDto
    {
        /// <summary>The service: <c>adjust</c>, …</summary>
        [JsonNameCamel] public string ProviderKey;

        /// <summary>The id at that service — for Adjust, the adid.</summary>
        [JsonNameCamel] public string ExternalId;

        /// <summary>Who recorded it: <c>sdk</c> (a game report), <c>admin</c> or <c>callback</c>.</summary>
        [JsonNameCamel] public string Source;

        /// <summary>First report, UTC.</summary>
        [JsonNameCamel] public DateTime FirstSeenAt;

        /// <summary>Latest report, UTC.</summary>
        [JsonNameCamel] public DateTime LastSeenAt;

        /// <summary>
        /// Adjust's attribution as last reported; set only when <see cref="ProviderKey"/> is <c>adjust</c>. Its fields
        /// are null while the install is organic or not attributed yet.
        /// </summary>
        [JsonNameCamel] public AdjustAttributionInfoDto Adjust;
    }

    /// <summary>Adjust's attribution of the install, as the server has it.</summary>
    [Serializable]
    public sealed class AdjustAttributionInfoDto
    {
        [JsonNameCamel] public string TrackerToken;
        [JsonNameCamel] public string TrackerName;
        [JsonNameCamel] public string Network;
        [JsonNameCamel] public string Campaign;
        [JsonNameCamel] public string Adgroup;
        [JsonNameCamel] public string Creative;
        [JsonNameCamel] public string ClickLabel;
    }
}
