using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class DayRewardDto
    {
        public int dayNumber;
        public RewardDataDto[] rewards;
        public RewardDataDto[] bonusRewards;
        public int bonusStreakThreshold;
        public bool isSpecialDay;
    }
}
