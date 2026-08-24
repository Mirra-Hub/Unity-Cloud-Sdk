using System;
using MirraCloud.Core.Groups.Dto.Response;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class UpdateRoleDto
    {
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public GroupPermissionsDto Permissions;
    }
}
