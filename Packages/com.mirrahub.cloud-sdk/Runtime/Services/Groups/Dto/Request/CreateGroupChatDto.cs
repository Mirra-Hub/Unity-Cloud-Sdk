using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class CreateGroupChatDto
    {
        /// <summary>Key of the chat template for the group chat. Required.</summary>
        [JsonNameCamel] public string ChatTemplateKey;
    }
}
