using System;
using Plugins.MirraCloud.Core.Services.Challenges.Enums;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed class ChallengeConfigDto
    {
        public string id;
        public string key;
        public string name;

        public double targetValue;
        public RewardMode rewardMode;

        public RewardRangeDto[] rewardsForFinishers;
        public RewardRangeDto[] rewardsForNonFinishers;

        public long duration;
        public int? finishersToEnd;

        public OrderType orderType;
        public UpdateStrategy updateStrategy;

        public bool isReset;
        public ResetIntervalType resetIntervalType;
        public int resetIntervalValue;
        public DateTime? nextResetDate;
        public DateTime? lastResetDate;

        public bool cohortsEnabled;
        public int cohortSize;

        public DateTime createdDate;
        public DateTime updatedDate;
    }
}
