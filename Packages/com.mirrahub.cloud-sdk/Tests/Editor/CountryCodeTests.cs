using MirraCloud.Core.Enums;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// An account that never set a country read as Afghanistan, the enum's zero value. <c>Undefined</c> took the zero
    /// slot and every country moved up by one — the same numbers as the server's <c>Country</c>.
    /// </summary>
    [TestFixture]
    public sealed class CountryCodeTests
    {
        [Test]
        public void The_zero_value_is_undefined()
        {
            Assert.That(default(CountryCode), Is.EqualTo(CountryCode.Undefined));
            Assert.That((int)CountryCode.Undefined, Is.EqualTo(0));
        }

        [Test]
        public void Countries_have_the_server_numbers()
        {
            Assert.That((int)CountryCode.Afghanistan, Is.EqualTo(1));
            Assert.That((int)CountryCode.AlandIslands, Is.EqualTo(2));
            Assert.That((int)CountryCode.Zimbabwe, Is.EqualTo(249));
            Assert.That(System.Enum.GetValues(typeof(CountryCode)).Length, Is.EqualTo(250));
        }

        [System.Serializable]
        public sealed class Holder
        {
            public CountryCode country;
        }

        [Test]
        public void An_account_without_a_country_reads_as_undefined()
        {
            var holder = JsonMapper.FromJson<Holder>("{\"country\":\"Undefined\"}");

            Assert.That(holder.country, Is.EqualTo(CountryCode.Undefined));
        }
    }
}
