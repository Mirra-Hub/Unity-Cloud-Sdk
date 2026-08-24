using System;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Enums;

namespace MirraCloud.Core.Leaderboard.Entities
{
    public class LeaderboardConfig
    {
        public readonly string Id;
        /// <summary>Business key — pass this to the service methods; the server resolves leaderboards by key.</summary>
        public readonly string Key;
        public readonly string Name;

        public readonly RewardRangeDto[] RewardsForPlaces;
    
        public readonly OrderType OrderType ;
        public readonly LeaderboardType Type;
        public readonly UpdateStrategy UpdateStrategy;
        public readonly RewardDistributionType RewardDistributionType;

        public readonly bool IsReset;
        
        public readonly ResetIntervalType ResetIntervalType;
        public readonly int ResetIntervalValue;
        public readonly DateTime? NextResetDate;
        public readonly DateTime? LastResetDate;

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
            NextResetDate = dto.nextResetDate;
            LastResetDate = dto.lastResetDate;
            CreatedDate = dto.createdDate;
            UpdatedDate = dto.updatedDate;
        }
    }
}
