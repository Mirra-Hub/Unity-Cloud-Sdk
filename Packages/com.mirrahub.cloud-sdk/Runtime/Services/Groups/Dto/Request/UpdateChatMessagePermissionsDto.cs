using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    /// <summary>
    /// The group admin's message permissions for the group chat. <c>null</c> = as in the chat template.
    /// Replaces all four at once.
    /// </summary>
    [Serializable]
    public class UpdateChatMessagePermissionsDto
    {
        [JsonNameCamel] public bool? EditOwn;
        [JsonNameCamel] public bool? DeleteOwn;
        [JsonNameCamel] public bool? EditOthers;
        [JsonNameCamel] public bool? DeleteOthers;
    }
}
