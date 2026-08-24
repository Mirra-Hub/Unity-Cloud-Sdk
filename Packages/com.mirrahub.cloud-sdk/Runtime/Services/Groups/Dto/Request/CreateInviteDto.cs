using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class CreateInviteDto
    {
        [JsonNameCamel] public string TargetPlayerId;
        [JsonNameCamel] public string InviteType;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
