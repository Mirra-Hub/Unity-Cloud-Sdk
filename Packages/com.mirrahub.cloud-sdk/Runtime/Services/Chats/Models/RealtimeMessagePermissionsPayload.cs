using System;
using MirraCloud.Core.Chats.Dto;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Models
{
    [Serializable]
    public sealed class RealtimeMessagePermissionsPayload
    {
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public ChatMessagePermissionsDto MessagePermissions;
    }
}
