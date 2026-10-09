using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Asks for a password reset code; fill the email or the username.</summary>
    [Serializable]
    public class RequestPasswordResetDto
    {
        public string Email;
        public string Username;
    }
}
