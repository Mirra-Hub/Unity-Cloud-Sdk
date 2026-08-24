using System;
using MirraCloud.Core.Leaderboard.Dto;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class PlayerRewardsDto
    {
        public string playerId;
        public RewardDataDto[] rewards;
    }
}

