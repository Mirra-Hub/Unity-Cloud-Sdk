using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Groups.Dto.Request
{
    [Serializable]
    public class JoinByKeyDto
    {
        [JsonNameCamel] public string SecretKey;
    }
}
