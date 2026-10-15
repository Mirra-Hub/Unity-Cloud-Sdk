using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    [Serializable]
    public sealed class ChatChannelDto
    {
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string Type;
        [JsonNameCamel] public ChatOwnerRefDto OwnerRef;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string Topic;
        [JsonNameCamel] public string TemplateKey;

        /// <summary>What the player may do with messages in this channel.</summary>
        [JsonNameCamel] public ChatMessagePermissionsDto MessagePermissions;

        /// <summary>The group admin's changes to the template's message permissions.</summary>
        [JsonNameCamel] public ChatMessagePermissionOverridesDto MessagePermissionOverrides;
        [JsonNameCamel] public string State;
        [JsonNameCamel] public long LastMessageNumber;
        [JsonNameCamel] public DateTime? LastMessageAt;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime UpdatedAt;
    }
}
