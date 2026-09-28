using System;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;

namespace MirraCloud.Core.Leaderboard.Dto
{
    /// <summary>
    /// One player's row on a leaderboard. The name, icon and country are the player's profile's own, as the server
    /// saw them at the player's last submitted score.
    /// </summary>
    [Serializable]
    public sealed record LeaderboardEntryDto
    {
        /// <summary>The player's profile id.</summary>
        public string playerId;

        /// <summary>The profile's nickname.</summary>
        public string playerName;

        /// <summary>The profile's avatar; null when the player has none.</summary>
        public IconKeyDto iconKey;

        /// <summary>The profile's country.</summary>
        public CountryCode countryCode;

        /// <summary>Place on the board, from 1. Players with equal scores get consecutive places.</summary>
        public int position;
        public double value;
    }
}
