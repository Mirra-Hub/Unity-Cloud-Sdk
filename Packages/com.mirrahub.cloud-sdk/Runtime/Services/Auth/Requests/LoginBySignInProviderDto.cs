using System;
using System.Collections.Generic;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// DTO for sign-in providers (Google / Apple / Yandex). The provider type is encoded in the URL
    /// (e.g. /login/google-sign-in), so this body only carries the verification payload:
    /// - AuthCode for OAuth code-flow providers (Yandex ID),
    /// - IdToken for ID-token providers (Google Sign-In, Sign In With Apple),
    /// - Extra for additional native data,
    /// - ExternalUserId can be set explicitly when the provider does not return it via the verifier.
    /// </summary>
    [Serializable]
    public class LoginBySignInProviderDto
    {
        public string ExternalUserId;
        public string AuthCode;
        public string IdToken;
        public Dictionary<string, string> Extra;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}
