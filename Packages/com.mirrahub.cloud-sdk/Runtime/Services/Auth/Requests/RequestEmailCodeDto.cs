using System;

namespace MirraCloud.Core.Auth
{
    /// <summary>Asks for a sign-in code to the email.</summary>
    [Serializable]
    public class RequestEmailCodeDto
    {
        public string Email;
        /// <summary>Whether signing in with the code may create an account; without it no letter goes to an unknown email.</summary>
        public bool CreateAccount;
        /// <summary>
        /// Language of the letter for a new player, a <see cref="Enums.LanguageCode"/> name; a known player gets the
        /// account's language. Null — English.
        /// </summary>
        public string Language;
    }
}
