using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public class RewardRangeDto
    {
        public double valueMin;
        public double valueMax;

        public int pLaceInLeaderboardMin;
        public int pLaceInLeaderboardMax;

        public RewardDataDto[] rewards;
    }
    
    [Serializable]
    public sealed record RewardDataDto
    {
        public string rewardId;
        public RewardType rewardType;
        public int count;
    }

    public enum RewardType
    {
        Currency,
        Item,
    }
}
