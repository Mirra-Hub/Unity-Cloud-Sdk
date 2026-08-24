using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class UpdateMemberRoleDto
    {
        [JsonNameCamel] public string RoleId;
    }
}
