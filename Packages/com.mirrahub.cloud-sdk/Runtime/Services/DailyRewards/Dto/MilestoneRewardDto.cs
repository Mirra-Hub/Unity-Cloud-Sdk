using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class MilestoneRewardDto
    {
        public int totalDays;
        public RewardDataDto[] rewards;
    }
}
