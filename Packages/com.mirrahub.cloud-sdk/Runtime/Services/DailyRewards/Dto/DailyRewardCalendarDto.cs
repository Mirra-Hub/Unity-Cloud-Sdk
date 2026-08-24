using System;
using MirraCloud.Core.DailyRewards.Enums;

namespace MirraCloud.Core.DailyRewards.Dto
{
    [Serializable]
    public sealed class DailyRewardCalendarDto
    {
        public string id;
        public string key;
        public string name;
        public string description;
        public bool isEnabled;
        public CalendarType calendarType;
        public int cycleLengthDays;
        public CycleCompletionBehavior cycleCompletionBehavior;
        public string nextCalendarId;
        public MissedDayBehavior missedDayBehavior;
        public int catchUpMaxDays;
        public bool requireManualClaim;
        public int resetHourUtc;
        public DayRewardDto[] dayRewards;
        public StreakBonusDto[] streakBonuses;
        public MilestoneRewardDto[] milestoneRewards;
        public string[] segmentKeys;
        public int priority;
        public bool isExclusive;
        public DateTime? startDate;
        public DateTime? endDate;
        public DateTime createdDate;
        public DateTime updatedDate;
    }
}
