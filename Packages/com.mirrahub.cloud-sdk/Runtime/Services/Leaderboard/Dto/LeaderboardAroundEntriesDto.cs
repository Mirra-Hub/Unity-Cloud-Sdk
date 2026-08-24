using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public class LeaderboardAroundEntriesDto
    {
        public LeaderboardEntryDto targetPlayer;
        public LeaderboardEntryDto[] pLayersAbove;
        public LeaderboardEntryDto[] playersBelow;
    }
}