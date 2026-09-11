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
        /// <summary>
        /// The account the session belongs to, as of this refresh. A restored session raises no
        /// <c>OnLogin</c>, so this is where <c>PlayerAccountService.PlayerAccountInfo</c> comes from for a
        /// returning player.
        /// </summary>
        [JsonNameCamel] public AccountDto PlayerInfo;
    }
}
