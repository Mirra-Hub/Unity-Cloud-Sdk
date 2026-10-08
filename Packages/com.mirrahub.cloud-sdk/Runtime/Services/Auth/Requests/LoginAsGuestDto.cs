using System;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class LoginAsGuestDto
    {
        public string GuestId;
        public bool CreateAccount;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
