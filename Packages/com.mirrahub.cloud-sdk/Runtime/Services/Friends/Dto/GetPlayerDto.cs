using System;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Friends.Dto
{
    [Serializable]
    public class GetPlayerDto
    {
        [JsonNameCamel] public string PlayerId;
        [JsonNameCamel] public ProfilePresenceStatus Status;
        [JsonNameCamel] public PlayerInfoDto PlayerInfo;
    }
}
