using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class TournamentEntriesDto
    {
        public string tournamentId;
        public string leagueId;
        public TournamentEntryDto[] entries;
    }
}

