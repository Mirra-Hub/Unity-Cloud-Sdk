using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginByDeviceIdDto
    {
        public string DeviceId;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}

