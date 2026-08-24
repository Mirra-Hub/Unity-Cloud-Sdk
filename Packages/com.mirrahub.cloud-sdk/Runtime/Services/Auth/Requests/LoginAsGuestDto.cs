using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginAsGuestDto
    {
        public string GuestId;
        public bool CreateAccount;
    }
}
