using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class ClaimDailyRewardResponseDto
    {
        public int dayNumberClaimed;
        public int newTotalClaimDays;
        public RewardDataDto[] baseRewards;
        public RewardDataDto[] streakBonusRewards;
        public RewardDataDto[] milestoneRewards;
        public bool cycleCompleted;
        public string nextCalendarId;
    }
}
