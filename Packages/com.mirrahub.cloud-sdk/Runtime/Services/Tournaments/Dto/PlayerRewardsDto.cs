using System;
using MirraCloud.Core.Leaderboard.Dto;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    [Obsolete("Tournament rewards are paid into Economy: EconomyService.GetPendingRewardsAsync returns them.")]
    public sealed class PlayerRewardsDto
    {
        public string playerId;
        public RewardDataDto[] rewards;
    }
}

