using System;
using MirraCloud.Json;

namespace Plugins.MirraCloud.Core.Services.PlayerAccount.Dto
{
    [Serializable]
    public class CreateProfileDto
    {
        [JsonNameCamel] public string Nickname;
        [JsonName("iconKey")] public string IconKey;
    }
}
