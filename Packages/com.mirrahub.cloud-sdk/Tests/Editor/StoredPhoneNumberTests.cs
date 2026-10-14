using MirraCloud.Core.Auth;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// What <c>PlayerAccountInfo.PhoneNumber</c> holds after <c>ChangePhoneAsync</c> succeeds. The server answers
    /// 204 and keeps the number in E.164 with the separators dropped; the SDK used to keep the number as typed, so
    /// <c>+7 (999) 123-45-67</c> stayed in the local info while the account had <c>+79991234567</c>.
    /// </summary>
    [TestFixture]
    public class StoredPhoneNumberTests
    {
        [TestCase("+7 (999) 123-45-67", "+79991234567")]
        [TestCase("+1.555.000.1122", "+15550001122")]
        [TestCase("  +442071234567 ", "+442071234567")]
        [TestCase("+79991234567", "+79991234567")]
        public void Separators_are_dropped_the_way_the_server_drops_them(string typed, string stored)
        {
            Assert.AreEqual(stored, AuthenticationService.StoredPhoneNumber(typed));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void A_blank_number_is_a_removed_one(string typed)
        {
            Assert.IsNull(AuthenticationService.StoredPhoneNumber(typed));
        }
    }
}
