using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Sets a new password with the reset code.</summary>
    [Serializable]
    public class ConfirmPasswordResetDto
    {
        public string ChallengeId;
        public string Code;
        public string NewPassword;
    }
}
