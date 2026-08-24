using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginByEmailDto
    {
        public string Email;
        public string Password;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}

