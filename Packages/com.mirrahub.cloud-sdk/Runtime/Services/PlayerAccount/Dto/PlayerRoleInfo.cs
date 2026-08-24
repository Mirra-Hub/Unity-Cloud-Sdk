using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    /// <summary>
    /// Player-role catalog entry for the current branch (see <c>PlayerAccountService.GetPlayerRolesAsync</c>).
    /// <see cref="Key"/> matches the values in <see cref="ProfileInfo.RoleKeys"/>.
    /// </summary>
    [Serializable]
    public class PlayerRoleInfo
    {
        [JsonNameCamel] public string Key;
        [JsonNameCamel] public string Name;
    }
}
