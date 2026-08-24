using System;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class DailyRewardStatusDto
    {
        public string calendarId;
        public string calendarKey;
        public string calendarName;
        public int cycleLengthDays;
        public int currentDayNumber;
        public int currentCycle;
        public int totalClaimDays;
        public DateTime? lastClaimDate;
        public bool isCompleted;
        public bool canClaimToday;
        public DateTime nextResetTime;
        public DayStatusDto[] days;
        public StreakBonusDto[] streakBonuses;
        public MilestoneProgressDto[] milestoneProgress;
    }
}
