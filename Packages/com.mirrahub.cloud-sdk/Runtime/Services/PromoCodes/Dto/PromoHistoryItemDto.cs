using System;

namespace MirraCloud.Core.PromoCodes.Dto
{
    [Serializable]
    public sealed class PromoHistoryItemDto
    {
        public string campaignId;
        public string campaignKey;
        public string campaignDisplayName;
        /// <summary>ISO-8601 UTC.</summary>
        public string redeemedAt;
        /// <summary>ISO-8601 UTC; <c>null</c> if the campaign didn't grant a timed effect.</summary>
        public string effectExpiresAt;
    }
}
