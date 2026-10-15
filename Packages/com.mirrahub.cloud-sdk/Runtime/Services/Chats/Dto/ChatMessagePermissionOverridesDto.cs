using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Dto
{
    /// <summary>
    /// The group admin's changes to the chat template's message permissions. <c>null</c> = as in the template.
    /// </summary>
    [Serializable]
    public sealed class ChatMessagePermissionOverridesDto
    {
        [JsonNameCamel] public bool? EditOwn;
        [JsonNameCamel] public bool? DeleteOwn;
        [JsonNameCamel] public bool? EditOthers;
        [JsonNameCamel] public bool? DeleteOthers;
    }
}
