using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    /// <summary>
    /// Request body for creating a chat (a <c>room</c> channel) from a chat template.
    /// <see cref="TemplateKey"/> is required; the channel is created according to that template
    /// and the caller is joined as its first member.
    /// </summary>
    [Serializable]
    public sealed class CreateChatChannelDto
    {
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string Topic;
        [JsonNameCamel] public string TemplateKey;
    }
}
