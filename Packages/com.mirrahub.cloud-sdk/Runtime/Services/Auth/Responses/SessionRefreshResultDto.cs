using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class SessionRefreshResultDto
    {
        [JsonNameCamel] public string AccountId;
        [JsonNameCamel] public string ProjectId;
        /// <summary>Freshly minted access token (JWT) the server issues on refresh — must replace the expired one.</summary>
        [JsonNameCamel] public string Token;
        [JsonNameCamel] public SessionInfoDto Session;
    }
}
