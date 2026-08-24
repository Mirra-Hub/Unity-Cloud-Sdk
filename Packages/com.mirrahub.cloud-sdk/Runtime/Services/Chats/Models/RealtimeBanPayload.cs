using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Models
{
    [Serializable]
    internal sealed class RealtimeBanPayload
    {
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string ProfileId;
        [JsonNameCamel] public string BannedBy;
    }
}
