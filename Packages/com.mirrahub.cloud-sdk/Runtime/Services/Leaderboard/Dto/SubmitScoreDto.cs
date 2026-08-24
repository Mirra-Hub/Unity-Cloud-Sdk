using System;

namespace MirraCloud.Core.Leaderboard.Dto
{
    [Serializable]
    public sealed record SubmitScoreDto
    {
        [MirraCloud.Json.JsonNameCamel] public string PlayerName;
        [MirraCloud.Json.JsonNameCamel] public double Value;
    }
}
