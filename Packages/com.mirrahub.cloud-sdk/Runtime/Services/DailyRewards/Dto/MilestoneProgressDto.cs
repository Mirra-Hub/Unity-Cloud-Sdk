using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class MilestoneProgressDto
    {
        public int totalDaysRequired;
        public bool isReached;
        public RewardDataDto[] rewards;
    }
}
