using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    [Serializable]
    public class GetAuthDataDto
    {
        [JsonNameCamel] public AuthResultStatus Status;
        [JsonNameCamel] public string Token;
        [JsonNameCamel] public AccountDto PlayerInfo;
        [JsonNameCamel] public AccountDto CurrentAccount;
        /// <summary>On a link conflict: the account the credentials belong to, as any player may see it.</summary>
        [JsonNameCamel] public PublicAccountDto ExistingAccount;
        [JsonNameCamel] public string GuestId;
        [JsonNameCamel] public SessionInfoDto Session;
        /// <summary>
        /// The account has no usable selected profile (none, or a banned one): the token carries no profile, and
        /// profile-scoped services refuse calls until the game selects or creates one.
        /// </summary>
        [JsonNameCamel] public bool ProfileSelectionRequired;
        /// <summary>
        /// On an OpenID or Google / Apple / Yandex sign-in link conflict: a one-time ticket for the verified identity,
        /// valid for 10 minutes, that <see cref="AuthenticationService.ResolveLinkConflictAsync"/> needs.
        /// </summary>
        [JsonNameCamel] public string CredentialTicket;
    }
}
