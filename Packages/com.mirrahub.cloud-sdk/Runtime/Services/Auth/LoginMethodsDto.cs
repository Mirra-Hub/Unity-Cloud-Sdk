using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// The sign-in methods the platform of this build offers right now
    /// (<see cref="AuthenticationService.GetLoginMethodsAsync"/>), so a game draws only the buttons that work.
    /// </summary>
    [Serializable]
    public class LoginMethodsDto
    {
        /// <summary>The platform the answer is for: <see cref="Configuration.PlatformKey"/>.</summary>
        [JsonNameCamel] public string PlatformKey;

        /// <summary>Enabled methods only, in the order the platform lists them in the console.</summary>
        [JsonNameCamel] public List<LoginMethodDto> Methods;
    }

    /// <summary>One sign-in method of a platform.</summary>
    [Serializable]
    public class LoginMethodDto
    {
        /// <summary>
        /// The kind as the server sends it (<c>guest</c>, <c>email</c>, <c>openid</c>, <c>google_play</c>, …).
        /// Kept as text so a kind added on the server later reads as <see cref="LoginMethodKind.Unknown"/> instead
        /// of failing the whole list.
        /// </summary>
        [JsonName("kind")] public string KindKey;

        /// <summary>
        /// For <see cref="LoginMethodKind.OpenId"/>, <see cref="LoginMethodKind.Google"/>,
        /// <see cref="LoginMethodKind.Apple"/> and <see cref="LoginMethodKind.Yandex"/>: the key to start the
        /// browser sign-in with (<see cref="AuthenticationService.LoginOpenIdAsync"/>). A platform may offer several
        /// OpenID providers, and this key tells them apart. Null for the other kinds.
        /// </summary>
        [JsonNameCamel] public string IntegrationKey;

        /// <summary>For <see cref="LoginMethodKind.OpenId"/>: the name to put on the button. Null for the other kinds.</summary>
        [JsonNameCamel] public string DisplayName;

        [JsonIgnore] public LoginMethodKind Kind => LoginMethodKinds.Parse(KindKey);

        /// <summary>A store sign-in (<see cref="LoginMethodKinds.IsStore"/>): <see cref="AuthenticationService.LoginPlatformAsync"/>.</summary>
        [JsonIgnore] public bool IsStore => LoginMethodKinds.IsStore(Kind);
    }
}
