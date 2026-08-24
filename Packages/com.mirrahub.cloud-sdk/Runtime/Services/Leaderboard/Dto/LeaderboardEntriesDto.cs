using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public class LeaderboardEntriesDto
    {
        public string leaderboardId;
        public LeaderboardEntryDto[] entries;
    }
}