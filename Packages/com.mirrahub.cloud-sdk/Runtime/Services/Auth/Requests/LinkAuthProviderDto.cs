using System;
using System.Collections.Generic;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// DTO for resolving a provider link conflict (<see cref="AuthenticationService.ResolveLinkConflictAsync"/>).
    /// Fill the credential fields of the provider in <see cref="ProviderType"/>; the rest stay null.
    /// </summary>
    /// <remarks>
    /// Names no platform: a store conflict (<c>ProviderType</c> = Platform) is resolved on the platform of the
    /// current session, the one it signed in on.
    /// </remarks>
    [Serializable]
    public class LinkAuthProviderDto
    {
        public int ProviderType;
        public string TargetAccountId;

        public string GuestId;
        public string DeviceId;
        public string Email;
        public string Login;
        public string Password;

        public string ExternalUserId;
        public string AuthCode;
        public string PlatformToken;
        public Dictionary<string, string> Extra;
        /// <summary>Required when the linked provider has to create a new account.</summary>
        public string Nickname;
        /// <summary>
        /// For an OpenID or Google / Apple / Yandex sign-in conflict: the <see cref="GetAuthDataDto.CredentialTicket"/>
        /// of the conflict answer. Left empty, the SDK fills in the ticket of the last conflict.
        /// </summary>
        public string CredentialTicket;
        /// <summary>What this client runs on; the SDK fills it.</summary>
        public SessionDeviceDto Device;
    }
}
