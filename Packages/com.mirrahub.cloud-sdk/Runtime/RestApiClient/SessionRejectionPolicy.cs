using System;
using System.Collections.Generic;
using MirraCloud.Core.Errors;

namespace MirraCloud.Core
{
    /// <summary>
    /// Tells a 401/403 that refuses the caller's session apart from a 401/403 that an endpoint gives as its
    /// answer to a perfectly valid session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the first kind is worth a session refresh. A new token fixes an expired or stale one; it does not fix
    /// a wrong password (<c>player_accounts.invalid_credentials</c>), a link conflict the caller is not a party to
    /// (<c>common.forbidden</c>) or an avatar change the project has switched off
    /// (<c>player_accounts.avatar_change_disabled</c>). Refreshing on those rotated the session and sent the
    /// request again only to get the same refusal.
    /// </para>
    /// <para>
    /// A refusal without the Cloud error envelope counts as a session problem: that is what the gateways answer
    /// for a missing, malformed or expired JWT (<c>{"error":"token expired"}</c>, or an empty body) before the
    /// request reaches any service.
    /// </para>
    /// </remarks>
    internal static class SessionRejectionPolicy
    {
        /// <summary>
        /// Codes a service returns when the token cannot act for this call: a claim the endpoint reads from it is
        /// missing or stale, or the session behind it is over. A refreshed token can change every one of these.
        /// </summary>
        private static readonly HashSet<string> SessionErrorCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            // No PlayerId / SelectedProfileId claim, or the token names another profile than the one addressed.
            CloudErrorCodes.CommonUnauthorized,
            CloudErrorCodes.PurchasesSelectedProfileRequired,
            CloudErrorCodes.PlayerAccountsSessionExpired,
            CloudErrorCodes.PlayerAccountsSessionMismatch,
            CloudErrorCodes.PlayerAccountsSessionProjectMismatch,
        };

        internal static bool IsAuthRejection(long httpCode)
        {
            return httpCode == 401 || httpCode == 403;
        }

        /// <summary>
        /// True when the refusal is about the session, so refreshing it and repeating the request can succeed.
        /// </summary>
        /// <param name="errors">The parsed error envelope; null when the body was not one.</param>
        internal static bool IsSessionRejection(IReadOnlyList<CloudApiError> errors)
        {
            if (errors == null)
            {
                return true;
            }

            var hasCode = false;
            for (var i = 0; i < errors.Count; i++)
            {
                var code = errors[i]?.Code;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (SessionErrorCodes.Contains(code))
                {
                    return true;
                }

                hasCode = true;
            }

            // An envelope without a single code says nothing about the session either way: treat it as no envelope.
            return hasCode == false;
        }
    }
}
