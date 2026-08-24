using System;
using MirraCloud.Core.PromoCodes.Enums;

namespace MirraCloud.Core.PromoCodes.Dto
{
    [Serializable]
    public sealed class GrantedRewardDto
    {
        public string rewardId;

        /// <summary>Which economy resource was granted. Arrives as a PascalCase string.</summary>
        public PromoRewardKind economyResourceKind;

        public int count;
    }
}
