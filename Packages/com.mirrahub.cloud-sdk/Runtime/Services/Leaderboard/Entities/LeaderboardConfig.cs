using System;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Enums;

namespace MirraCloud.Core.Leaderboard.Entities
{
    public class LeaderboardConfig
    {
        /// <summary>The config's id in the console. The service methods take <see cref="Key"/>.</summary>
        public readonly string Id;
        /// <summary>Business key — pass this to the service methods; the server resolves leaderboards by key.</summary>
        public readonly string Key;
        public readonly string Name;

        /// <summary>
        /// What the board pays out when a session ends. The rewards arrive in Economy: see
        /// <c>EconomyService.GetPendingRewardsAsync</c> and <c>ClaimRewardsAsync</c>.
        /// </summary>
        public readonly RewardRangeDto[] RewardsForPlaces;
    
        public readonly OrderType OrderType ;
        public readonly LeaderboardType Type;
        public readonly UpdateStrategy UpdateStrategy;
        public readonly RewardDistributionType RewardDistributionType;

        public readonly bool IsReset;
        
        public readonly ResetIntervalType ResetIntervalType;
        public readonly int ResetIntervalValue;

        /// <summary>The time of day of the reset, UTC.</summary>
        public readonly int ResetTimeHour;
        public readonly int ResetTimeMinute;

        /// <summary>
        /// When the board resets next, UTC, the time of day included. Null when auto-reset is off or before the
        /// board's first score.
        /// </summary>
        public readonly DateTime? NextResetDate;

        /// <summary>The day of the last reset, UTC. Null before the board's first score.</summary>
        public readonly DateTime? LastResetDate;

        /// <summary>Players compete in groups of <see cref="CohortSize"/> instead of one table.</summary>
        public readonly bool CohortsEnabled;
        public readonly int CohortSize;

        public readonly DateTime CreatedDate;
        public readonly DateTime UpdatedDate;
        
        public LeaderboardConfig(LeaderboardConfigDto dto)
        {
            Id = dto.id;
            Key = dto.key;
            Name = dto.name;
            RewardsForPlaces = dto.rewardsForPlaces;
            OrderType = dto.orderType;
            Type = dto.type;
            UpdateStrategy = dto.updateStrategy;
            RewardDistributionType = dto.rewardDistributionType;
            IsReset = dto.isReset;
            ResetIntervalType = dto.resetIntervalType;
            ResetIntervalValue = dto.resetIntervalValue;
            ResetTimeHour = dto.resetTimeHour;
            ResetTimeMinute = dto.resetTimeMinute;
            NextResetDate = dto.nextResetDate;
            LastResetDate = dto.lastResetDate;
            CohortsEnabled = dto.cohortsEnabled;
            CohortSize = dto.cohortSize;
            CreatedDate = dto.createdDate;
            UpdatedDate = dto.updatedDate;
        }
    }
}
