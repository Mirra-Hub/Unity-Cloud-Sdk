using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginByUsernameDto
    {
        public string Login;
        public string Password;
        public bool CreateAccount;
        /// <summary>Required when CreateAccount=true and the account does not yet exist.</summary>
        public string Nickname;
    }
}

