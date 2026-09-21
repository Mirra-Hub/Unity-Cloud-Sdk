namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// A way to sign in that a platform can offer (<see cref="LoginMethodDto.Kind"/>). Each value names the SDK
    /// call that signs the player in with it.
    /// </summary>
    public enum LoginMethodKind
    {
        /// <summary>A kind this SDK version does not know yet. Its wire value is in <see cref="LoginMethodDto.KindKey"/>.</summary>
        Unknown = 0,

        /// <summary><see cref="AuthenticationService.LoginGuestAsync"/>.</summary>
        Guest,

        /// <summary><see cref="AuthenticationService.LoginDeviceAsync"/>.</summary>
        Device,

        /// <summary><see cref="AuthenticationService.LoginEmailAsync"/>.</summary>
        Email,

        /// <summary><see cref="AuthenticationService.LoginUsernameAsync"/>.</summary>
        Username,

        /// <summary>An OpenID provider: <see cref="AuthenticationService.LoginOpenIdAsync"/> with <see cref="LoginMethodDto.IntegrationKey"/>.</summary>
        OpenId,

        /// <summary>
        /// Google: <see cref="AuthenticationService.LoginGoogleSignInAsync"/> with a token from the native Google SDK,
        /// or <see cref="AuthenticationService.LoginOpenIdAsync"/> with <see cref="LoginMethodDto.IntegrationKey"/>.
        /// </summary>
        Google,

        /// <summary>
        /// Sign in with Apple: <see cref="AuthenticationService.LoginSignInWithAppleAsync"/>, or
        /// <see cref="AuthenticationService.LoginOpenIdAsync"/> with <see cref="LoginMethodDto.IntegrationKey"/>.
        /// </summary>
        Apple,

        /// <summary>
        /// Yandex ID: <see cref="AuthenticationService.LoginYandexSignInAsync"/>, or
        /// <see cref="AuthenticationService.LoginOpenIdAsync"/> with <see cref="LoginMethodDto.IntegrationKey"/>.
        /// </summary>
        Yandex,

        /// <summary>Google Play Games: <see cref="AuthenticationService.LoginPlatformAsync"/>.</summary>
        GooglePlay,

        /// <summary>VK Games: <see cref="AuthenticationService.LoginPlatformAsync"/>.</summary>
        VkGames,

        /// <summary>Yandex Games: <see cref="AuthenticationService.LoginPlatformAsync"/>.</summary>
        YandexGames,

        /// <summary>Apple Game Center: <see cref="AuthenticationService.LoginPlatformAsync"/>.</summary>
        AppleGameCenter
    }

    /// <summary>The wire keys of <see cref="LoginMethodKind"/> (<c>google_play</c>, <c>vk_games</c>, …).</summary>
    public static class LoginMethodKinds
    {
        /// <summary>
        /// The kind a wire key names; <see cref="LoginMethodKind.Unknown"/> for a key this SDK version does not know,
        /// so a kind added on the server later does not break the list.
        /// </summary>
        public static LoginMethodKind Parse(string key)
        {
            switch (key)
            {
                case "guest": return LoginMethodKind.Guest;
                case "device": return LoginMethodKind.Device;
                case "email": return LoginMethodKind.Email;
                case "username": return LoginMethodKind.Username;
                case "openid": return LoginMethodKind.OpenId;
                case "google": return LoginMethodKind.Google;
                case "apple": return LoginMethodKind.Apple;
                case "yandex": return LoginMethodKind.Yandex;
                case "google_play": return LoginMethodKind.GooglePlay;
                case "vk_games": return LoginMethodKind.VkGames;
                case "yandex_games": return LoginMethodKind.YandexGames;
                case "apple_game_center": return LoginMethodKind.AppleGameCenter;
                default: return LoginMethodKind.Unknown;
            }
        }

        /// <summary>
        /// True for the store kinds. A platform has at most one of them, and the player signs in with it through
        /// <see cref="AuthenticationService.LoginPlatformAsync"/>, which needs the store's own SDK on the device.
        /// </summary>
        public static bool IsStore(LoginMethodKind kind)
        {
            return kind == LoginMethodKind.GooglePlay
                   || kind == LoginMethodKind.VkGames
                   || kind == LoginMethodKind.YandexGames
                   || kind == LoginMethodKind.AppleGameCenter;
        }
    }
}
