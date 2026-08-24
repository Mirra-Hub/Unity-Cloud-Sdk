using System;
using Plugins.MirraCloud.Core.Services.Challenges.Dto;
using Plugins.MirraCloud.Core.Services.Challenges.Enums;

namespace Plugins.MirraCloud.Core.Services.Challenges.Entities
{
    public class ChallengeConfig
    {
        public readonly string Id;
        /// <summary>Business key — pass this to the service methods; the server resolves challenges by key.</summary>
        public readonly string Key;
        public readonly string Name;

        public readonly double TargetValue;
        public readonly RewardMode RewardMode;

        public readonly RewardRangeDto[] RewardsForFinishers;
        public readonly RewardRangeDto[] RewardsForNonFinishers;

        public readonly long Duration;
        public readonly int? FinishersToEnd;

        public readonly OrderType OrderType;
        public readonly UpdateStrategy UpdateStrategy;

        public readonly bool IsReset;
        public readonly ResetIntervalType ResetIntervalType;
        public readonly int ResetIntervalValue;
        public readonly DateTime? NextResetDate;
        public readonly DateTime? LastResetDate;

        public readonly bool CohortsEnabled;
        public readonly int CohortSize;

        public readonly DateTime CreatedDate;
        public readonly DateTime UpdatedDate;

        public ChallengeConfig(ChallengeConfigDto dto)
        {
            Id = dto.id;
            Key = dto.key;
            Name = dto.name;
            TargetValue = dto.targetValue;
            RewardMode = dto.rewardMode;
            RewardsForFinishers = dto.rewardsForFinishers;
            RewardsForNonFinishers = dto.rewardsForNonFinishers;
            Duration = dto.duration;
            FinishersToEnd = dto.finishersToEnd;
            OrderType = dto.orderType;
            UpdateStrategy = dto.updateStrategy;
            IsReset = dto.isReset;
            ResetIntervalType = dto.resetIntervalType;
            ResetIntervalValue = dto.resetIntervalValue;
            NextResetDate = dto.nextResetDate;
            LastResetDate = dto.lastResetDate;
            CohortsEnabled = dto.cohortsEnabled;
            CohortSize = dto.cohortSize;
            CreatedDate = dto.createdDate;
            UpdatedDate = dto.updatedDate;
        }
    }
}
