using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Auth
{
    [Serializable]
    public sealed class RealtimeSessionInfo
    {
        [JsonNameCamel] public string WsUrl;
        [JsonNameCamel] public string RtToken;
        [JsonNameCamel] public DateTime ExpiresAt;
    }
}
