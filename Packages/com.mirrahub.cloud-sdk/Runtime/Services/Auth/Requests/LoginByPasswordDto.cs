using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Password sign-in. To sign in, fill <see cref="Email"/> or <see cref="Username"/>; to register
    /// (<see cref="CreateAccount"/>) fill what the platform takes — <see cref="LoginMethodDto.Password"/> says which.
    /// </summary>
    [Serializable]
    public class LoginByPasswordDto
    {
        public string Email;
        /// <summary>The account username, with or without its <c>acc_</c> prefix; compared exactly.</summary>
        public string Username;
        public string Password;
        public bool CreateAccount;
        /// <summary>Applied once, if this sign-in creates the account.</summary>
        public LoginOptionsDto Options;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
