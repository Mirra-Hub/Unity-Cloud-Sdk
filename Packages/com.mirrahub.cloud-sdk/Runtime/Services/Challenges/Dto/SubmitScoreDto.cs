using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.Challenges.Dto
{
    [Serializable]
    public sealed record SubmitScoreDto
    {
        [JsonNameCamel] public string PlayerName;
        [JsonNameCamel] public double Value;
    }
}
