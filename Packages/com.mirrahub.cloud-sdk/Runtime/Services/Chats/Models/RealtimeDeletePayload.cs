using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Models
{
    [Serializable]
    public sealed class RealtimeDeletePayload
    {
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string MessageId;
    }
}
