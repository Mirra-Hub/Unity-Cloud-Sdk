using System;

namespace Plugins.MirraCloud.Core.Services.Tournaments.Dto
{
    [Serializable]
    public sealed class PlayerLeagueMetaDto
    {
        public string tournamentId;
        public string currentLeagueTableId;
        public int currentLeagueTableIndex;
    }
}

