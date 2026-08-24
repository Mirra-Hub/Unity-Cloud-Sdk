using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class TournamentEntryDto
    {
        public string playerId;
        public string playerName;
        public int position;
        public double value;
    }
}

