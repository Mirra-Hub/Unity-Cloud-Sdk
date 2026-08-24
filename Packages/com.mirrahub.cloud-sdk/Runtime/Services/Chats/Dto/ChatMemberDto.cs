using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    [Serializable]
    public sealed class ChatMemberDto
    {
        [JsonNameCamel] public string ProfileId;
        [JsonNameCamel] public DateTime JoinedAt;
        [JsonNameCamel] public long LastReadMessageNumber;
        [JsonNameCamel] public DateTime? LastReadAt;
    }
}
