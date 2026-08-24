using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    [Serializable]
    public sealed class ChatMessageDto
    {
        [JsonNameCamel] public string MessageId;
        [JsonNameCamel] public long Number;
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string SenderId;
        [JsonNameCamel] public string Body;
        [JsonNameCamel] public JsonValue Metadata;
        [JsonNameCamel] public string[] TaggedMembers;
        [JsonNameCamel] public string TargetMessageId;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime? EditedAt;
        [JsonNameCamel] public DateTime? DeletedAt;
    }
}
