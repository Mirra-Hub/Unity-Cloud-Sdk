using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class MemberDto
    {
        [JsonNameCamel] public string ProfileId;
        [JsonNameCamel] public string RoleId;
        [JsonNameCamel] public string RoleName;
        [JsonNameCamel] public bool IsOwner;
        [JsonNameCamel] public DateTime JoinedAt;
    }
}
