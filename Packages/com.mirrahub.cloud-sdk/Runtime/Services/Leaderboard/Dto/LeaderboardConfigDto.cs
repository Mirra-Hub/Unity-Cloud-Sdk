using System;
using MirraCloud.Core.Leaderboard.Enums;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed class LeaderboardConfigDto
    {
        /// <summary>The config's id in the console. The SDK addresses a board by <see cref="key"/>.</summary>
        public string id;
        public string key;
        public string name;

        public RewardRangeDto[] rewardsForPlaces;
    
        public OrderType orderType ;
        public LeaderboardType type;
        public UpdateStrategy updateStrategy;
        public RewardDistributionType rewardDistributionType;

        public bool isReset;
        
        public ResetIntervalType resetIntervalType;
        public int resetIntervalValue;

        /// <summary>The time of day of the reset, UTC.</summary>
        public int resetTimeHour;
        public int resetTimeMinute;

        /// <summary>
        /// When the board resets next, UTC, the time of day included — for a countdown. Null when auto-reset is off or
        /// before the board's first score.
        /// </summary>
        public DateTime? nextResetDate;

        /// <summary>The day of the last reset, UTC. Null before the board's first score.</summary>
        public DateTime? lastResetDate;

        /// <summary>Players compete in groups of <see cref="cohortSize"/> instead of one table.</summary>
        public bool cohortsEnabled;
        public int cohortSize;

        public DateTime createdDate;
        public DateTime updatedDate;
    }
}
