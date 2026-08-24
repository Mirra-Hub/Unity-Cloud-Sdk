using System;
using MirraCloud.Core.Groups.Dto.Response;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class CreateRoleDto
    {
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public GroupPermissionsDto Permissions;
    }
}
