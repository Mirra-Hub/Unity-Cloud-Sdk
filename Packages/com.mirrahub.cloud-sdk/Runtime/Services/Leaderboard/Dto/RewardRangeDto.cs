using System;
using MirraCloud.Core.Economy;

namespace MirraCloud.Core.Leaderboard.Dto
{
    /// <summary>
    /// The rewards for a range of places (<see cref="Enums.RewardDistributionType.ByPlace"/>, from 1) or of scores
    /// (<see cref="Enums.RewardDistributionType.ByScore"/>), both ends included.
    /// </summary>
    [Serializable]
    public class RewardRangeDto
    {
        public double valueMin;
        public double valueMax;

        public RewardDataDto[] rewards;

        [Obsolete("Never filled: the range is valueMin..valueMax.")]
        public int pLaceInLeaderboardMin;

        [Obsolete("Never filled: the range is valueMin..valueMax.")]
        public int pLaceInLeaderboardMax;
    }

    /// <summary>One economy resource granted to a player whose place or score falls in the range.</summary>
    [Serializable]
    public sealed record RewardDataDto
    {
        /// <summary>
        /// The key of the economy resource — the one <see cref="EconomyService"/> and the rewards it hands out use.
        /// Null when the branch no longer has that resource.
        /// </summary>
        public string rewardKey;

        public EconomyResourceKind economyResourceKind;
        public int count;

        /// <summary>The stable id of the economy resource, as the console stores it.</summary>
        public string rewardId;

        [Obsolete("Never filled: use economyResourceKind.")]
        public RewardType rewardType;
    }

    [Obsolete("Use MirraCloud.Core.Economy.EconomyResourceKind (RewardDataDto.economyResourceKind).")]
    public enum RewardType
    {
        Currency,
        Item,
    }
}
