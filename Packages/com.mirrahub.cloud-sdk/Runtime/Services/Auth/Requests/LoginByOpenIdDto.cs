using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginByOpenIdDto
    {
        public string UserId;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}

