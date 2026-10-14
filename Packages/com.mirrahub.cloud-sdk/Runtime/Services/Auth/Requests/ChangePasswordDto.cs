using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Changes the password of the signed-in account.</summary>
    [Serializable]
    public class ChangePasswordDto
    {
        public string CurrentPassword;
        public string NewPassword;
    }
}
