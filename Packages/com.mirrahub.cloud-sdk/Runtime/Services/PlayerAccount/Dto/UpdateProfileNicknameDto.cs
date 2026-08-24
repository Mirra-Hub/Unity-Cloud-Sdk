using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class UpdateProfileNicknameDto
    {
        [JsonNameCamel] public string Nickname;
    }
}

