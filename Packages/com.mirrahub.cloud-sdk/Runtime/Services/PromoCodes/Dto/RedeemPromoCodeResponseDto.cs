using System;
using System.Collections.Generic;
using MirraCloud.Core.PromoCodes.Enums;

namespace MirraCloud.Core.PromoCodes.Dto
{
    [Serializable]
    public sealed class RedeemPromoCodeResponseDto
    {
        /// <summary>Outcome of the redemption. Non-success statuses arrive with HTTP 200.</summary>
        public RedemptionStatus status;

        public string campaignId;
        public string campaignKey;
        public string campaignDisplayName;

        public List<GrantedRewardDto> rewards = new List<GrantedRewardDto>();
        public List<GrantedEffectDto> effects = new List<GrantedEffectDto>();
    }
}
