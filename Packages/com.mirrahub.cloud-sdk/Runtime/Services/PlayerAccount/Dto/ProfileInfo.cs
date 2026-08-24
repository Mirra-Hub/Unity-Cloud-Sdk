using System;
using MirraCloud.Json;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class ProfileInfo
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string AccountId;
        [JsonNameCamel] public string Nickname;
        /// <summary>Mutable, project-unique public handle (stored with the pr_ prefix).</summary>
        [JsonNameCamel] public string Username;
        [JsonNameCamel] public Gender Gender;
        [JsonName("iconKey")] public IconKeyDto IconKey;
        [JsonNameCamel] public string IconUrl;
        [JsonNameCamel] public string Status;
        [JsonNameCamel] public string[] SegmentKeys;
        [JsonNameCamel] public string[] AbTestKeys;
        /// <summary>Player-role catalog keys assigned from the admin. Resolve names via <c>GetPlayerRolesAsync</c>.</summary>
        [JsonNameCamel] public string[] RoleKeys;
        [JsonNameCamel] public DateTime LastLogin;
        [JsonNameCamel] public DateTime CreatedDate;
        [JsonNameCamel] public DateTime UpdatedDate;
    }
}
