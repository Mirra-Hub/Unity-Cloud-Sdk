using System;
using System.Collections.Generic;

namespace MirraCloud.Core.PromoCodes.Dto
{
    [Serializable]
    public sealed class PromoActiveEffectDto
    {
        public string key;
        public Dictionary<string, string> metadata = new Dictionary<string, string>();
        public string campaignId;
        /// <summary>ISO-8601 UTC.</summary>
        public string grantedAt;
        /// <summary>ISO-8601 UTC. <c>null</c> for permanent effects.</summary>
        public string expiresAt;
    }
}
