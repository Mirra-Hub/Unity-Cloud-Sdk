using System;
using System.Security.Cryptography;
using System.Text;

namespace MirraCloud.Core.Auth.OpenId
{
    /// <summary>
    /// The proof key of a browser sign-in: the server hands the result only to whoever shows the verifier whose
    /// challenge started the sign-in, so a result key that leaks through the return address is useless alone.
    /// </summary>
    public static class OpenIdPkce
    {
        /// <summary>A new random code verifier: 32 random bytes, base64url, 43 characters.</summary>
        public static string CreateVerifier()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Base64Url(bytes);
        }

        /// <summary>The challenge of <paramref name="verifier"/>: <c>base64url(SHA-256(verifier))</c>.</summary>
        public static string ChallengeOf(string verifier)
        {
            using (var sha = SHA256.Create())
            {
                return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            }
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
