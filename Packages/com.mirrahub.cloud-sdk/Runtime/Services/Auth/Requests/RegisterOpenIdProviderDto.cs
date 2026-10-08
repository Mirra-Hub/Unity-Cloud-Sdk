using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class RegisterOpenIdProviderDto
    {
        public string SuccessUrl;
        /// <summary><c>base64url(SHA-256(code verifier))</c> of this sign-in; the result is redeemed with the verifier.</summary>
        public string CodeChallenge;
        public SessionDeviceDto Device;
    }
}

