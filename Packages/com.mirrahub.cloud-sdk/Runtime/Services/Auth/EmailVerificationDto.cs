using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// A code sent by email that the player has to enter to go on. Send <see cref="ChallengeId"/> back with the code;
    /// <see cref="AuthenticationService.ResendEmailCodeAsync"/> sends a new one.
    /// </summary>
    [Serializable]
    public class EmailVerificationDto
    {
        /// <summary>The request the code belongs to.</summary>
        [JsonNameCamel] public string ChallengeId;

        /// <summary>Where the letter went, masked (<c>pl***@example.com</c>) — show it to the player.</summary>
        [JsonNameCamel] public string Email;

        /// <summary>Seconds until the code expires.</summary>
        [JsonNameCamel] public int ExpiresInSeconds;

        /// <summary>Seconds before a new code may be requested.</summary>
        [JsonNameCamel] public int ResendAfterSeconds;
    }
}
