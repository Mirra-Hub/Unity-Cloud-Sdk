using System.Collections.Generic;
using NUnit.Framework;
using MirraCloud.Core.Errors;
using MirraCloud.Json;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Which 401/403 answers make <c>RestApiClient</c> refresh the session and repeat the call.
    ///
    /// <para>
    /// The refresh used to follow every 401/403. Once login, link and profile endpoints started answering with
    /// typed refusals — a wrong password is a 401, a forbidden link or a disabled avatar change a 403 — each of
    /// those rotated the session and sent the request a second time for the same answer. Only a refused session
    /// is worth refreshing: a gateway's JWT refusal (no envelope) or a code that says the token cannot act here.
    /// </para>
    /// <para>
    /// The envelope is read the way the client reads it, from the camelCase body every Cloud host writes. The
    /// error DTOs once expected PascalCase members, so <c>errors</c> never matched, every failure arrived without
    /// codes, and this decision could not have been made at all.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SessionRejectionPolicyTests
    {
        private static List<CloudApiError> ReadEnvelope(string body)
        {
            return new JsonService().FromJson<ErrorResponseDto>(body)?.Errors;
        }

        private static string Envelope(string code)
        {
            return "{\"errors\":[{\"code\":\"" + code + "\",\"message\":\"refused\",\"data\":{}}]}";
        }

        [Test]
        public void Reads_the_error_envelope_from_the_camelCase_body_the_hosts_write()
        {
            var errors = ReadEnvelope(
                "{\"errors\":[{\"code\":\"integrations.integration_in_use\",\"message\":\"In use.\","
                + "\"data\":{\"integrationKey\":\"gp\",\"usages\":[{\"module\":\"Platforms\",\"ownerKey\":\"android\"}]}}]}");

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Code, Is.EqualTo(CloudErrorCodes.IntegrationsIntegrationInUse));
            Assert.That(errors[0].Message, Is.EqualTo("In use."));
            Assert.That((string)errors[0].Data["integrationKey"], Is.EqualTo("gp"));
            Assert.That((string)errors[0].Data["usages"][0]["ownerKey"], Is.EqualTo("android"));
        }

        /// <summary>The gateways refuse a missing, malformed or expired JWT before any service sees it.</summary>
        [Test]
        public void A_gateway_refusal_without_an_envelope_is_a_session_problem()
        {
            var errors = ReadEnvelope("{\"error\":\"token expired\"}");

            Assert.That(errors, Is.Null);
            Assert.That(SessionRejectionPolicy.IsSessionRejection(errors), Is.True);
        }

        [Test]
        public void An_empty_or_unreadable_body_is_a_session_problem()
        {
            Assert.That(SessionRejectionPolicy.IsSessionRejection(null), Is.True);
        }

        [Test]
        public void An_envelope_that_carries_no_code_is_a_session_problem()
        {
            Assert.That(SessionRejectionPolicy.IsSessionRejection(ReadEnvelope("{\"errors\":[]}")), Is.True);
            Assert.That(SessionRejectionPolicy.IsSessionRejection(
                ReadEnvelope("{\"errors\":[{\"code\":null,\"message\":\"x\",\"data\":{}}]}")), Is.True);
        }

        [TestCase(CloudErrorCodes.CommonUnauthorized)]
        [TestCase(CloudErrorCodes.PurchasesSelectedProfileRequired)]
        [TestCase(CloudErrorCodes.PlayerAccountsSessionExpired)]
        [TestCase(CloudErrorCodes.PlayerAccountsSessionMismatch)]
        [TestCase(CloudErrorCodes.PlayerAccountsSessionProjectMismatch)]
        public void A_code_that_says_the_token_cannot_act_is_a_session_problem(string code)
        {
            Assert.That(SessionRejectionPolicy.IsSessionRejection(ReadEnvelope(Envelope(code))), Is.True);
        }

        [TestCase(CloudErrorCodes.PlayerAccountsInvalidCredentials)]
        [TestCase(CloudErrorCodes.PlayerAccountsExternalAuthInvalidIdToken)]
        [TestCase(CloudErrorCodes.PlayerAccountsExternalAuthInvalidSignature)]
        [TestCase(CloudErrorCodes.PlayerAccountsProviderNotEnabled)]
        [TestCase(CloudErrorCodes.PlayerAccountsAvatarChangeDisabled)]
        [TestCase(CloudErrorCodes.PlayerAccountsPlatformDisabled)]
        [TestCase(CloudErrorCodes.CommonForbidden)]
        [TestCase(CloudErrorCodes.GroupsPlayerBanned)]
        [TestCase(CloudErrorCodes.LeaderboardsParticipationRequired)]
        public void An_endpoint_refusing_a_valid_session_is_not_a_session_problem(string code)
        {
            Assert.That(SessionRejectionPolicy.IsSessionRejection(ReadEnvelope(Envelope(code))), Is.False);
        }

        [Test]
        public void A_session_code_anywhere_in_the_envelope_wins()
        {
            var errors = ReadEnvelope(
                "{\"errors\":[{\"code\":\"common.forbidden\",\"message\":\"a\",\"data\":{}},"
                + "{\"code\":\"common.unauthorized\",\"message\":\"b\",\"data\":{}}]}");

            Assert.That(SessionRejectionPolicy.IsSessionRejection(errors), Is.True);
        }

        [TestCase(401L, true)]
        [TestCase(403L, true)]
        [TestCase(400L, false)]
        [TestCase(404L, false)]
        [TestCase(409L, false)]
        [TestCase(500L, false)]
        [TestCase(0L, false)]
        public void Only_401_and_403_are_auth_rejections(long httpCode, bool expected)
        {
            Assert.That(SessionRejectionPolicy.IsAuthRejection(httpCode), Is.EqualTo(expected));
        }
    }
}
