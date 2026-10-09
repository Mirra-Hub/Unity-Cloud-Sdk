using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>
    /// Values a sign-in applies once, when it creates a new account; a returning player's account is not changed.
    /// </summary>
    [Serializable]
    public class LoginOptionsDto
    {
        /// <summary>
        /// The new account's nickname. The project's nickname rules apply (pattern, banned words, uniqueness): a
        /// nickname they refuse fails the sign-in. Empty — the server generates one.
        /// </summary>
        public string Nickname;

        /// <summary>The options of a sign-in with this nickname, or none when it is empty.</summary>
        public static LoginOptionsDto WithNickname(string nickname) =>
            string.IsNullOrWhiteSpace(nickname) ? null : new LoginOptionsDto { Nickname = nickname.Trim() };
    }
}
