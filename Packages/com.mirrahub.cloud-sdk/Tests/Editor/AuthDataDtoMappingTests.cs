using NUnit.Framework;
using MirraCloud.Core.Auth;
using MirraCloud.Json;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// The sign-in answer held for email confirmation and the account's contacts, read the way the runtime reads
    /// them. The server writes the status as its name, and a registration held for a code has no token: the game
    /// has to see the status and the code request, or it shows a failed sign-in instead of the code form.
    /// </summary>
    [TestFixture]
    public class AuthDataDtoMappingTests
    {
        [Test]
        public void Reads_a_sign_in_held_for_email_confirmation()
        {
            var dto = JsonMapper.FromJson<GetAuthDataDto>(
                "{\"status\":\"EmailVerificationRequired\",\"token\":null,\"session\":null,"
                + "\"emailVerification\":{\"challengeId\":\"c0ffee\",\"email\":\"pl***@example.com\","
                + "\"expiresInSeconds\":900,\"resendAfterSeconds\":60}}");

            Assert.That(dto.Status, Is.EqualTo(AuthResultStatus.EmailVerificationRequired));
            Assert.That(dto.Token, Is.Null);
            Assert.That(dto.EmailVerification, Is.Not.Null);
            Assert.That(dto.EmailVerification.ChallengeId, Is.EqualTo("c0ffee"));
            Assert.That(dto.EmailVerification.Email, Is.EqualTo("pl***@example.com"));
            Assert.That(dto.EmailVerification.ExpiresInSeconds, Is.EqualTo(900));
            Assert.That(dto.EmailVerification.ResendAfterSeconds, Is.EqualTo(60));
        }

        [Test]
        public void Reads_the_account_email_and_phone()
        {
            var dto = JsonMapper.FromJson<AccountDto>(
                "{\"id\":\"a1\",\"username\":\"acc_player\",\"email\":\"player@example.com\","
                + "\"isEmailVerified\":true,\"phoneNumber\":\"+79991234567\",\"isPhoneNumberVerified\":false}");

            Assert.That(dto.Email, Is.EqualTo("player@example.com"));
            Assert.That(dto.IsEmailVerified, Is.True);
            Assert.That(dto.PhoneNumber, Is.EqualTo("+79991234567"));
            Assert.That(dto.IsPhoneNumberVerified, Is.False);
        }
    }
}
