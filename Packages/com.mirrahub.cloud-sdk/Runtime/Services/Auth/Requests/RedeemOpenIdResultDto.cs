using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class RedeemOpenIdResultDto
    {
        [JsonNameCamel] public string ResultKey;
        [JsonNameCamel] public string CodeVerifier;
    }
}
