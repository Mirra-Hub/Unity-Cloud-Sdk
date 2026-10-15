using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class CreateGroupDto
    {
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string[] Tag;
        [JsonNameCamel] public string Description;
        [JsonNameCamel] public string Avatar;
        [JsonNameCamel] public string Visibility;
        [JsonNameCamel] public string JoinPolicy;
        [JsonNameCamel] public int MaxMembers;
        [JsonNameCamel] public string Metadata;
        [JsonNameCamel] public bool CreateChat;

        /// <summary>Key of the chat template for the group chat. Required when <see cref="CreateChat"/> is set.</summary>
        [JsonNameCamel] public string ChatTemplateKey;
        [JsonNameCamel] public bool AutoJoinMembers;
    }
}
