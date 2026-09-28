using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public class LeaderboardEntriesDto
    {
        public string leaderboardKey;
        public LeaderboardEntryDto[] entries;

        [Obsolete("Never filled: the server answers with leaderboardKey.")]
        public string leaderboardId;
    }
}
