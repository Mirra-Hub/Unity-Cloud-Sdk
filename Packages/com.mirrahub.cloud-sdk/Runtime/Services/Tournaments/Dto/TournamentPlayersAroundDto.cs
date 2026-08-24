using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class TournamentPlayersAroundDto
    {
        public TournamentEntryDto targetPlayer;
        public TournamentEntryDto[] pLayersAbove;
        public TournamentEntryDto[] playersBelow;
    }
}

