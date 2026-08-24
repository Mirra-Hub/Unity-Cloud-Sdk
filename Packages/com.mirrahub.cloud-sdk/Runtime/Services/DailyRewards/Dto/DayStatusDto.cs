using System;
using MirraCloud.Core.DailyRewards.Enums;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class DayStatusDto
    {
        public int dayNumber;
        public DailyRewardClaimStatus status;
        public RewardDataDto[] rewards;
        public RewardDataDto[] bonusRewards;
        public bool isSpecialDay;
    }
}
