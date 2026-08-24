using System;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Friends.Dto
{
    [Serializable]
    public sealed class PlayerInfoDto
    {
        [JsonNameCamel] public string Nickname;
        [JsonName("iconKey")] public IconKeyDto IconKey;
        [JsonNameCamel] public AccountStatus Status;
        [JsonNameCamel] public DateTime LastLogin;
        [JsonNameCamel] public DateTime CreatedDate;
        [JsonNameCamel] public DateTime UpdatedDate;
    }
}

