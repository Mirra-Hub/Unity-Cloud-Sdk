namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Where the sign-in lives in <see cref="Storage.IStorage"/>. A renamed key signs every player out and turns
    /// every guest into a new one, so these are only ever added to.
    /// </summary>
    internal static class AuthStorageKeys
    {
        /// <summary>The only way back into a guest account: without it the next guest sign-in makes a new one.</summary>
        public const string GuestId = "auth/guest_id";

        /// <summary>Single-use: every refresh brings a new one, and the old one is refused from then on.</summary>
        public const string RefreshToken = "auth/refresh_token";
    }
}
