using System;
using MirraCloud.Core.DailyRewards.Enums;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class StreakBonusDto
    {
        public int streakDays;
        public StreakBonusType bonusType;
        public double multiplier;
        public RewardDataDto[] rewards;
    }
}
