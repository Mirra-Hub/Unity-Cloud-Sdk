using System;
using System.Collections.Generic;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Unified DTO for /login/platform and /link/platform: the store's proof of who the player is (AuthCode for
    /// OAuth flows, PlatformToken for native SDK signatures, Extra for VK / Yandex Games launch params and the
    /// Game Center signature).
    /// </summary>
    /// <remarks>
    /// Names no platform: the store is the single store sign-in of the platform the request is made on
    /// (<see cref="Configuration.PlatformKey"/> on login, the session's platform on link). The player's store id
    /// is always the one the server verified; <see cref="ExternalUserId"/> is read only by Game Center, whose
    /// signed payload contains it.
    /// </remarks>
    [Serializable]
    public class LoginByPlatformDto
    {
        public string ExternalUserId;
        public string AuthCode;
        public string PlatformToken;
        public Dictionary<string, string> Extra;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}
