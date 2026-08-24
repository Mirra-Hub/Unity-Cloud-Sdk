using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class JoinRequestDto
    {
        [JsonNameCamel] public string RequestId;
        [JsonNameCamel] public string GroupId;
        [JsonNameCamel] public string SourcePlayerId;
        [JsonNameCamel] public string Status;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
