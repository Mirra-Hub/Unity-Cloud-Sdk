using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    [Serializable]
    public sealed class ChatOwnerRefDto
    {
        [JsonNameCamel] public string Type;
        [JsonNameCamel] public string Id;
    }
}
