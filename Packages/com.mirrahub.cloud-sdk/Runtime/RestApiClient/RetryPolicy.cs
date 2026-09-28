using System;
using System.Globalization;
using UnityEngine.Networking;

namespace MirraCloud.Core
{
    /// <summary>
    /// Decides whether a failed request is sent again, and after what pause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client used to repeat every failed request once, at once. A refusal (403 "join first", 422 "invalid
    /// score") was sent twice for the same answer; a POST whose response was lost on the way back was applied
    /// twice — a score on a "Total" leaderboard counted double; and a 429 was answered with another request the
    /// gateway refused as well, spending the player's quota twice.
    /// </para>
    /// <para>
    /// Only what cannot do harm is repeated now:
    /// a network failure or a 502/503/504, for methods that are safe to repeat (GET, HEAD, PUT, DELETE, OPTIONS)
    /// and for POST/PATCH calls marked <see cref="RestRequestConfig.Idempotent"/> (reads sent as POST), after a
    /// growing pause; a 429 whatever the method — the gateway refused it before any service saw it — when the
    /// gateway asks for a short wait; never another 4xx, which the same request would get again.
    /// </para>
    /// <para>
    /// Every repeat counts against <see cref="RestRequestConfig.MaxRetries"/> (1 by default, 0 turns repeats off),
    /// together with the repeat after a session refresh.
    /// </para>
    /// </remarks>
    internal static class RetryPolicy
    {
        /// <summary>The pause before the first repeat of a transient failure; each further repeat doubles it.</summary>
        internal const float BaseDelaySeconds = 0.5f;

        internal const float MaxDelaySeconds = 4f;

        /// <summary>The longest <c>Retry-After</c> worth waiting for; a longer one is handed to the caller as the 429.</summary>
        internal const float MaxRetryAfterSeconds = 10f;

        /// <summary>The pause after a 429 that did not say how long to wait.</summary>
        internal const float DefaultRetryAfterSeconds = 1f;

        /// <param name="method">HTTP method of the request.</param>
        /// <param name="idempotent">The request is safe to repeat although its method is not (a read sent as POST).</param>
        /// <param name="httpCode">Status of the response; 0 when no response arrived.</param>
        /// <param name="networkResult">How the transfer ended.</param>
        /// <param name="retryCount">Repeats already made.</param>
        /// <param name="maxRetries">Repeats allowed.</param>
        /// <param name="disableRetry">The caller turned repeats off.</param>
        /// <param name="retryAfter">The <c>Retry-After</c> header of the response, in seconds; null when absent.</param>
        /// <param name="delaySeconds">How long to wait before the repeat.</param>
        /// <returns>True when the request is to be sent again after <paramref name="delaySeconds"/>.</returns>
        internal static bool ShouldRetry(
            string method,
            bool idempotent,
            long httpCode,
            UnityWebRequest.Result networkResult,
            int retryCount,
            int maxRetries,
            bool disableRetry,
            string retryAfter,
            out float delaySeconds)
        {
            delaySeconds = 0f;

            if (disableRetry || retryCount >= maxRetries)
            {
                return false;
            }

            if (httpCode == 429)
            {
                delaySeconds = ParseRetryAfter(retryAfter);
                return delaySeconds <= MaxRetryAfterSeconds;
            }

            var transient = httpCode is 502 or 503 or 504 ||
                            (httpCode <= 0 && networkResult == UnityWebRequest.Result.ConnectionError);
            if (transient == false || (idempotent == false && IsSafeToRepeat(method) == false))
            {
                return false;
            }

            delaySeconds = Math.Min(MaxDelaySeconds, BaseDelaySeconds * (float)Math.Pow(2, retryCount));
            return true;
        }

        /// <summary>Methods whose repeat leaves the server as one call would (RFC 9110, idempotent methods).</summary>
        internal static bool IsSafeToRepeat(string method)
        {
            switch (method?.ToUpperInvariant())
            {
                case "GET":
                case "HEAD":
                case "PUT":
                case "DELETE":
                case "OPTIONS":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Seconds from a <c>Retry-After</c> header. Only the delay form is read — the gateways send seconds; an
        /// HTTP date, a missing or an unreadable value fall back to <see cref="DefaultRetryAfterSeconds"/>.
        /// </summary>
        internal static float ParseRetryAfter(string retryAfter)
        {
            if (string.IsNullOrWhiteSpace(retryAfter) ||
                float.TryParse(retryAfter.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) == false ||
                float.IsNaN(seconds) || seconds < 0f)
            {
                return DefaultRetryAfterSeconds;
            }

            return seconds;
        }
    }
}
