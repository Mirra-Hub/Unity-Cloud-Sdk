using System;
using MirraCloud.Core.Leaderboard.Dto;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed record TournamentTableDto
    {
        public string id;
        public string name;

        public int leagueUpThreshold;
        public int leagueDownThreshold;

        public RewardRangeDto[] rewardsForPlaces;
    }
}