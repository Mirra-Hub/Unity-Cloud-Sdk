using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>The code from a letter, sent back with the request it belongs to.</summary>
    [Serializable]
    public class ConfirmEmailCodeDto
    {
        /// <summary><see cref="EmailVerificationDto.ChallengeId"/> of the request.</summary>
        public string ChallengeId;
        public string Code;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
