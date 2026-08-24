using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class SessionInfoDto
    {
        [JsonNameCamel] public string SessionId;
        [JsonNameCamel] public string RefreshToken;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
