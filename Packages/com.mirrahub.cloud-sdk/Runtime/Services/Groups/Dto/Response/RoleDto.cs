using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class RoleDto
    {
        [JsonNameCamel] public string RoleId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public bool IsOwner;
        [JsonNameCamel] public GroupPermissionsDto Permissions;
    }
}
