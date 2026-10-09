using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Adds a password to the signed-in account, with the email and/or username it will be used with.</summary>
    [Serializable]
    public class LinkPasswordDto
    {
        public string Email;
        public string Username;
        public string Password;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
