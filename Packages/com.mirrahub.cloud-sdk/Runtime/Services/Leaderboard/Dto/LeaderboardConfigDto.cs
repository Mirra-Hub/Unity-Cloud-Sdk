using System;
using MirraCloud.Core.Leaderboard.Enums;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed class LeaderboardConfigDto
    {
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
        public DateTime? nextResetDate;
        public DateTime? lastResetDate;

        public DateTime createdDate;
        public DateTime updatedDate;
    }
}
