using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Response
{
    [Serializable]
    public class BanDto
    {
        [JsonNameCamel] public string ProfileId;
        [JsonNameCamel] public string Reason;
        [JsonNameCamel] public DateTime BannedAt;
    }
}
