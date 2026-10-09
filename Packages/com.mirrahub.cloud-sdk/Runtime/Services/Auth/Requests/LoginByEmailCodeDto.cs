using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Sign-in by the code sent to the email (<see cref="AuthenticationService.RequestLoginCodeAsync"/>).</summary>
    [Serializable]
    public class LoginByEmailCodeDto
    {
        public string ChallengeId;
        public string Code;
        public bool CreateAccount;
        /// <summary>Applied once, if this sign-in creates the account.</summary>
        public LoginOptionsDto Options;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
