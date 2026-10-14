using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginByDeviceIdDto
    {
        public string DeviceId;
        public bool CreateAccount;
        /// <summary>Applied once, if this sign-in creates the account.</summary>
        public LoginOptionsDto Options;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}

