using System;
using MirraCloud.Core.Tournaments.Enums;
using Plugins.MirraCloud.Core.Services.Tournaments.Dto;

namespace Plugins.MirraCloud.Core.Services.Tournaments
{
    [Serializable]
    public class TournamentConfig
    {
        public readonly string Id;
        /// <summary>Business key — pass this to the service methods; the server resolves tournaments by key.</summary>
        public readonly string Key;
        public readonly string Name;
        
        public readonly TournamentTableDto[] tables;
        
        public readonly OrderType OrderType ;
        public readonly TournamentsType Type;
        public readonly UpdateStrategy UpdateStrategy;
        public readonly global::MirraCloud.Core.Leaderboard.Enums.RewardDistributionType RewardDistributionType;

        public readonly bool IsReset;
        
        public readonly ResetIntervalType ResetIntervalType;
        public readonly int ResetIntervalValue;
        public readonly DateTime? NextResetDate;
        public readonly DateTime? LastResetDate;

        public readonly DateTime CreatedDate;
        public readonly DateTime UpdatedDate;

        public TournamentConfig(TournamentConfigDto dto)
        {
            Id = dto.id;
            Key = dto.key;
            Name = dto.name;
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

            tables = dto.tables;
        }
    }
}
