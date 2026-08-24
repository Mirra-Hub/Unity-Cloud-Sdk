using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class BanPlayerDto
    {
        [JsonNameCamel] public string AccountId;
        [JsonNameCamel] public string Reason;
    }
}
