using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class CreateInviteKeyDto
    {
        [JsonNameCamel] public string InviteType;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
