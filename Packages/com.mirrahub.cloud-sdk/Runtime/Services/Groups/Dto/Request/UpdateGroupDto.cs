using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class UpdateGroupDto
    {
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string[] Tag;
        [JsonNameCamel] public string Description;
        [JsonNameCamel] public string Avatar;
        [JsonNameCamel] public string Visibility;
        [JsonNameCamel] public string JoinPolicy;
        [JsonNameCamel] public int MaxMembers;
        [JsonNameCamel] public string Metadata;
    }
}
