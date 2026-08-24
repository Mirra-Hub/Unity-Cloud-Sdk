using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class InviteDto
    {
        [JsonNameCamel] public string InviteId;
        [JsonNameCamel] public string GroupId;
        [JsonNameCamel] public string TargetPlayerId;
        [JsonNameCamel] public string Status;
        [JsonNameCamel] public string InviteType;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
