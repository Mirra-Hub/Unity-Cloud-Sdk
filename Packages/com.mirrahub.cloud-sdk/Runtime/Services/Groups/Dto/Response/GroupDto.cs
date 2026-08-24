using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class GroupDto
    {
        [JsonNameCamel] public string GroupId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string[] Tag;
        [JsonNameCamel] public string Description;
        [JsonNameCamel] public string Avatar;
        [JsonNameCamel] public string OwnerId;
        [JsonNameCamel] public string Visibility;
        [JsonNameCamel] public string JoinPolicy;
        [JsonNameCamel] public int MaxMembers;
        [JsonNameCamel] public int MemberCount;
        [JsonNameCamel] public string Metadata;
        [JsonNameCamel] public ChatConfigDto ChatConfig;
        [JsonNameCamel] public DateTime CreatedAt;
        [JsonNameCamel] public DateTime UpdatedAt;
    }
}
