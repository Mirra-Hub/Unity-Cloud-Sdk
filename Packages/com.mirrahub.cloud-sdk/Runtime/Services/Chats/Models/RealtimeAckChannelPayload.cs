using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Models
{
    [Serializable]
    internal sealed class RealtimeAckChannelPayload
    {
        [JsonNameCamel] public string RoomId;
    }
}
