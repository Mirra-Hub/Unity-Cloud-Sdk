using System;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Server-side composition of the player's nickname.
    /// Base is what the player typed; Suffix is the server-generated random suffix
    /// (null when the suffix is not active for this account); Displayed is what the UI
    /// should render and is computed by the server based on project settings.
    /// </summary>
    [Serializable]
    public class AccountNicknameDto
    {
        [JsonNameCamel] public string Base;
        [JsonNameCamel] public string Suffix;
        [JsonNameCamel] public string Displayed;
    }

    [Serializable]
    public class AccountDto
    {
        [JsonNameCamel] public string Id;
        [JsonNameCamel] public string ScopeId;
        [JsonNameCamel] public AccountNicknameDto Nickname;
        /// <summary>Mutable, project-unique public handle (stored with the acc_ prefix).</summary>
        [JsonNameCamel] public string Username;
        [JsonNameCamel] public Gender Gender;
        [JsonNameCamel] public int Age;
        [JsonName("iconKey")] public IconKeyDto IconKey;
        /// <summary>Public Dicebear-rendered URL when the account uses a Dicebear preset; null otherwise.</summary>
        [JsonNameCamel] public string AvatarUrl;
        [JsonNameCamel] public CountryCode Country;
        [JsonNameCamel] public LanguageCode LanguageCode;
        [JsonNameCamel] public string TimeZone;
        [JsonNameCamel] public string[] SegmentKeys;
        [JsonNameCamel] public string[] AbTestKeys;
        [JsonNameCamel] public string Status;
        [JsonNameCamel] public DateTime LastLoginDate;
        [JsonNameCamel] public DateTime CreatedDate;
        [JsonNameCamel] public DateTime UpdatedDate;
        [JsonNameCamel] public int TotalActiveDays;
        [JsonNameCamel] public int ConsecutiveActiveDays;
        [JsonNameCamel] public int MaxConsecutiveActiveDays;
        [JsonNameCamel] public int TotalSessions;
    }
}
