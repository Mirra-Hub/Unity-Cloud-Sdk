using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class InviteKeyDto
    {
        [JsonNameCamel] public string InviteKeyId;
        [JsonNameCamel] public string SecretKey;
        [JsonNameCamel] public string InviteType;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
