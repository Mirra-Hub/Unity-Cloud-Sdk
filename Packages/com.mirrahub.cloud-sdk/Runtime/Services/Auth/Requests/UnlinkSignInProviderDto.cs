using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Body of <c>DELETE …/unlink/{google-sign-in|sign-in-with-apple|yandex-sign-in}</c>: the sign-in is addressed
    /// by the player's id at the provider. No token is sent: only a sign-in of the caller's own account can be
    /// removed.
    /// </summary>
    [Serializable]
    public class UnlinkSignInProviderDto
    {
        [JsonNameCamel] public string ExternalUserId;
    }
}
