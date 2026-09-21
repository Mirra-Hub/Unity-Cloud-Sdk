using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Attribution.Dto
{
    /// <summary>
    /// What the game reports from the Adjust SDK: the device id (<see cref="Adid"/>) and, once Adjust has attributed
    /// the install, the attribution. Field names are those of Adjust's <c>AdjustAttribution</c>.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Adid"/> is required. A report with no attribution field keeps what the server has stored (the
    /// adid often reaches the game before the attribution does); a report with at least one field replaces the stored
    /// attribution as a whole.
    /// </remarks>
    [Serializable]
    public sealed class AdjustAttributionDto
    {
        /// <summary>Adjust device id. Up to 128 characters, no whitespace.</summary>
        [JsonNameCamel] public string Adid;

        [JsonNameCamel] public string TrackerToken;
        [JsonNameCamel] public string TrackerName;
        [JsonNameCamel] public string Network;
        [JsonNameCamel] public string Campaign;
        [JsonNameCamel] public string Adgroup;
        [JsonNameCamel] public string Creative;
        [JsonNameCamel] public string ClickLabel;

        /// <summary>Whether any attribution field (everything but <see cref="Adid"/>) is set.</summary>
        [JsonIgnore] public bool HasAttribution =>
            !string.IsNullOrWhiteSpace(TrackerToken) || !string.IsNullOrWhiteSpace(TrackerName) ||
            !string.IsNullOrWhiteSpace(Network) || !string.IsNullOrWhiteSpace(Campaign) ||
            !string.IsNullOrWhiteSpace(Adgroup) || !string.IsNullOrWhiteSpace(Creative) ||
            !string.IsNullOrWhiteSpace(ClickLabel);
    }
}
