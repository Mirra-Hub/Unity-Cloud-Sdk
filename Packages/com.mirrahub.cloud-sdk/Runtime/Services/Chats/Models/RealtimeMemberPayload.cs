using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Models
{
    [Serializable]
    internal sealed class RealtimeMemberPayload
    {
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string ProfileId;
    }
}
