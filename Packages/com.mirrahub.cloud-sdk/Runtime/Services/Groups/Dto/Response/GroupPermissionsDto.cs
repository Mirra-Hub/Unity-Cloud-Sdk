using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class GroupPermissionsDto
    {
        [JsonNameCamel] public bool CanInvite;
        [JsonNameCamel] public bool CanKick;
        [JsonNameCamel] public bool CanApplyRequests;
        [JsonNameCamel] public bool CanUpdateGroupData;
        [JsonNameCamel] public bool CanDeleteGroup;
        [JsonNameCamel] public bool CanAddRoleToOthers;
    }
}
