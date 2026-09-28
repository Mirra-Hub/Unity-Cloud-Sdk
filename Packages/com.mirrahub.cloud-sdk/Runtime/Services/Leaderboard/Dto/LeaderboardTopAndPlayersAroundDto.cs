using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public class LeaderboardTopAndPlayersAroundDto
    {
        public string leaderboardKey;

        public LeaderboardEntryDto[] top;
        public LeaderboardAroundEntriesDto playersAround;

        [Obsolete("Never filled: the server answers with leaderboardKey.")]
        public string leaderboardId;
    }
}
