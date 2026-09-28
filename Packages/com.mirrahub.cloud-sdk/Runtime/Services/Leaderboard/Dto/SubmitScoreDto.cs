using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    /// <summary>
    /// The score only: the name, icon and country on the board are taken from the player's profile on the server.
    /// </summary>
    [Serializable]
    public sealed record SubmitScoreDto
    {
        [MirraCloud.Json.JsonNameCamel] public double Value;
    }
}
