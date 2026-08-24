using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed record LeaderboardEntryDto
    {
        public string playerId;

        public string playerName;

        public int position;
        public double value;
    }
}