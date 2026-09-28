using NUnit.Framework;
using UnityEngine.Networking;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Which failed requests <c>RestApiClient</c> sends again, and after what pause.
    ///
    /// <para>
    /// Every non-2xx answer used to be repeated once, at once. A refused submit (403 "join first", 422) went out
    /// twice for the same answer; a POST whose response was lost was applied twice — a "Total" leaderboard counted
    /// the score double; a 429 was answered with a second request the gateway refused as well.
    /// </para>
    /// </summary>
    [TestFixture]
    public class RetryPolicyTests
    {
        private const UnityWebRequest.Result Connection = UnityWebRequest.Result.ConnectionError;
        private const UnityWebRequest.Result Protocol = UnityWebRequest.Result.ProtocolError;

        private static bool Retry(string method, long code, UnityWebRequest.Result result, out float delay,
            bool idempotent = false, int retryCount = 0, int maxRetries = 1, bool disableRetry = false,
            string retryAfter = null)
        {
            return RetryPolicy.ShouldRetry(method, idempotent, code, result, retryCount, maxRetries, disableRetry,
                retryAfter, out delay);
        }

        [TestCase("GET")]
        [TestCase("HEAD")]
        [TestCase("PUT")]
        [TestCase("DELETE")]
        public void A_safe_method_is_repeated_after_a_network_failure(string method)
        {
            Assert.That(Retry(method, 0, Connection, out var delay), Is.True);
            Assert.That(delay, Is.EqualTo(RetryPolicy.BaseDelaySeconds));
        }

        [TestCase(502L)]
        [TestCase(503L)]
        [TestCase(504L)]
        public void A_safe_method_is_repeated_after_a_gateway_hiccup(long code)
        {
            Assert.That(Retry("GET", code, Protocol, out _), Is.True);
        }

        /// <summary>A POST that timed out may have been applied: sending it again could apply it twice.</summary>
        [TestCase("POST", 0L)]
        [TestCase("POST", 503L)]
        [TestCase("PATCH", 0L)]
        [TestCase("PATCH", 502L)]
        public void A_write_is_not_repeated_after_a_failure_it_may_have_survived(string method, long code)
        {
            Assert.That(Retry(method, code, code == 0 ? Connection : Protocol, out _), Is.False);
        }

        /// <summary>Reads sent as POST — a list in the body — are marked idempotent and behave like a GET.</summary>
        [Test]
        public void A_post_marked_idempotent_is_repeated_like_a_get()
        {
            Assert.That(Retry("POST", 0, Connection, out _, idempotent: true), Is.True);
            Assert.That(Retry("POST", 504, Protocol, out _, idempotent: true), Is.True);
        }

        [TestCase(400L)]
        [TestCase(401L)]
        [TestCase(403L)]
        [TestCase(404L)]
        [TestCase(409L)]
        [TestCase(422L)]
        [TestCase(500L)]
        public void A_refusal_or_a_server_error_is_never_repeated(long code)
        {
            Assert.That(Retry("GET", code, Protocol, out _), Is.False);
            Assert.That(Retry("POST", code, Protocol, out _, idempotent: true), Is.False);
        }

        /// <summary>The gateway refuses a 429 before any service sees it, so even a write is safe to send again.</summary>
        [Test]
        public void A_rate_limited_write_is_repeated_once_after_the_wait_the_gateway_asks_for()
        {
            Assert.That(Retry("POST", 429, Protocol, out var delay, retryAfter: "6"), Is.True);
            Assert.That(delay, Is.EqualTo(6f));

            Assert.That(Retry("POST", 429, Protocol, out _, retryCount: 1, retryAfter: "6"), Is.False);
        }

        [Test]
        public void A_rate_limit_that_asks_for_a_long_wait_is_handed_to_the_caller()
        {
            Assert.That(Retry("GET", 429, Protocol, out _, retryAfter: "60"), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("soon")]
        [TestCase("Wed, 21 Oct 2026 07:28:00 GMT")]
        public void A_rate_limit_without_a_readable_wait_is_repeated_after_a_second(string retryAfter)
        {
            Assert.That(Retry("GET", 429, Protocol, out var delay, retryAfter: retryAfter), Is.True);
            Assert.That(delay, Is.EqualTo(RetryPolicy.DefaultRetryAfterSeconds));
        }

        [Test]
        public void Zero_retries_turn_repeats_off()
        {
            Assert.That(Retry("GET", 0, Connection, out _, maxRetries: 0), Is.False);
            Assert.That(Retry("GET", 429, Protocol, out _, maxRetries: 0), Is.False);
        }

        [Test]
        public void A_caller_can_turn_repeats_off()
        {
            Assert.That(Retry("GET", 503, Protocol, out _, disableRetry: true), Is.False);
        }

        [Test]
        public void Each_further_repeat_waits_twice_as_long_up_to_a_cap()
        {
            Retry("GET", 503, Protocol, out var second, retryCount: 1, maxRetries: 10);
            Retry("GET", 503, Protocol, out var third, retryCount: 2, maxRetries: 10);
            Retry("GET", 503, Protocol, out var late, retryCount: 8, maxRetries: 10);

            Assert.That(second, Is.EqualTo(RetryPolicy.BaseDelaySeconds * 2));
            Assert.That(third, Is.EqualTo(RetryPolicy.BaseDelaySeconds * 4));
            Assert.That(late, Is.EqualTo(RetryPolicy.MaxDelaySeconds));
        }
    }
}
