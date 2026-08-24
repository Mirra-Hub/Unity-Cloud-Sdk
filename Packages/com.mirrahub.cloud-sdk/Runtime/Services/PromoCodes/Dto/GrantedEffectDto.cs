using System;
using System.Collections.Generic;

namespace MirraCloud.Core.PromoCodes.Dto
{
    [Serializable]
    public sealed class GrantedEffectDto
    {
        public string key;
        public Dictionary<string, string> metadata = new Dictionary<string, string>();

        /// <summary>ISO-8601 UTC timestamp. <c>null</c> for permanent effects.</summary>
        public string expiresAt;
    }
}
