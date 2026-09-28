using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    [Obsolete("Leaderboard rewards are paid into Economy: EconomyService.GetPendingRewardsAsync returns them.")]
    public sealed class PlayerRewardsDto
    {
        public string playerId;
        public RewardDataDto[] rewards;
    }
}

