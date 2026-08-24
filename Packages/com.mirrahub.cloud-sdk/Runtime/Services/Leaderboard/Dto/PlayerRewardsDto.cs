using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed class PlayerRewardsDto
    {
        public string playerId;
        public RewardDataDto[] rewards;
    }
}

