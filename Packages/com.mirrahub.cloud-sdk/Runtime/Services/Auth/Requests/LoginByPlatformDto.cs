using System;
using System.Collections.Generic;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Unified DTO for /login/platform and /link/platform.
    /// Identifies the player by (PlatformId, ExternalUserId) and optionally carries
    /// platform-specific verification data (AuthCode for OAuth flows, PlatformToken for
    /// native SDK signatures, Extra for VK launch params, etc.).
    /// </summary>
    [Serializable]
    public class LoginByPlatformDto
    {
        public string PlatformId;
        public string ExternalUserId;
        public string AuthCode;
        public string PlatformToken;
        public Dictionary<string, string> Extra;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}
