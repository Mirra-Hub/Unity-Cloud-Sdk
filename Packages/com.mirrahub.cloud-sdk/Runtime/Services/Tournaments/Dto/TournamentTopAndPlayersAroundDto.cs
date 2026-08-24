using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class TournamentTopAndPlayersAroundDto
    {
        public string tournamentId;
        public string tableId;

        public TournamentEntryDto[] top;
        public TournamentPlayersAroundDto playersAround;
    }
}

