using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class GroupListItemDto
    {
        [JsonNameCamel] public string GroupId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string Description;
        [JsonNameCamel] public string Avatar;
        [JsonNameCamel] public string Visibility;
        [JsonNameCamel] public string JoinPolicy;
        [JsonNameCamel] public int MemberCount;
        [JsonNameCamel] public int MaxMembers;
    }
}
