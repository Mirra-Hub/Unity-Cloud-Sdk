using NUnit.Framework;
using MirraCloud.Core.Auth;
using MirraCloud.Json;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// The <c>login-methods</c> answer, read the way the runtime reads it.
    ///
    /// <para>
    /// A game draws its sign-in buttons from this list, so two silent failures matter. Member names are matched
    /// case-sensitively: a camelCase body read into unmarked PascalCase fields gives a list of blank methods, and
    /// the screen shows no buttons without any error. And the kinds are snake_case wire keys that no enum name
    /// matches: parsed straight into an enum, the first kind the server adds later would fail the whole list.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LoginMethodsDtoMappingTests
    {
        /// <summary>A real answer: every kind of entry the server writes, with nulls serialized.</summary>
        private const string Payload =
            "{\"platformKey\":\"android\",\"methods\":["
            + "{\"kind\":\"guest\",\"integrationKey\":null,\"displayName\":null},"
            + "{\"kind\":\"openid\",\"integrationKey\":\"sso\",\"displayName\":\"Company SSO\"},"
            + "{\"kind\":\"google\",\"integrationKey\":\"google-main\",\"displayName\":null},"
            + "{\"kind\":\"vk_games\",\"integrationKey\":null,\"displayName\":null}]}";

        [Test]
        public void Reads_the_whole_answer()
        {
            var dto = JsonMapper.FromJson<LoginMethodsDto>(Payload);

            Assert.That(dto.PlatformKey, Is.EqualTo("android"));
            Assert.That(dto.Methods, Has.Count.EqualTo(4));

            Assert.That(dto.Methods[0].Kind, Is.EqualTo(LoginMethodKind.Guest));
            Assert.That(dto.Methods[0].IntegrationKey, Is.Null);

            Assert.That(dto.Methods[1].Kind, Is.EqualTo(LoginMethodKind.OpenId));
            Assert.That(dto.Methods[1].IntegrationKey, Is.EqualTo("sso"));
            Assert.That(dto.Methods[1].DisplayName, Is.EqualTo("Company SSO"));

            Assert.That(dto.Methods[2].Kind, Is.EqualTo(LoginMethodKind.Google));
            Assert.That(dto.Methods[2].IntegrationKey, Is.EqualTo("google-main"));

            Assert.That(dto.Methods[3].Kind, Is.EqualTo(LoginMethodKind.VkGames));
            Assert.That(dto.Methods[3].KindKey, Is.EqualTo("vk_games"));
            Assert.That(dto.Methods[3].IsStore, Is.True);
        }

        [Test]
        public void A_kind_this_version_does_not_know_reads_as_unknown_and_keeps_the_rest()
        {
            var dto = JsonMapper.FromJson<LoginMethodsDto>(
                "{\"platformKey\":\"web\",\"methods\":["
                + "{\"kind\":\"steam\",\"integrationKey\":null,\"displayName\":null},"
                + "{\"kind\":\"email\",\"integrationKey\":null,\"displayName\":null}]}");

            Assert.That(dto.Methods, Has.Count.EqualTo(2));
            Assert.That(dto.Methods[0].Kind, Is.EqualTo(LoginMethodKind.Unknown));
            Assert.That(dto.Methods[0].KindKey, Is.EqualTo("steam"));
            Assert.That(dto.Methods[1].Kind, Is.EqualTo(LoginMethodKind.Email));
        }

        [TestCase("guest", LoginMethodKind.Guest)]
        [TestCase("device", LoginMethodKind.Device)]
        [TestCase("email", LoginMethodKind.Email)]
        [TestCase("username", LoginMethodKind.Username)]
        [TestCase("openid", LoginMethodKind.OpenId)]
        [TestCase("google", LoginMethodKind.Google)]
        [TestCase("apple", LoginMethodKind.Apple)]
        [TestCase("yandex", LoginMethodKind.Yandex)]
        [TestCase("google_play", LoginMethodKind.GooglePlay)]
        [TestCase("vk_games", LoginMethodKind.VkGames)]
        [TestCase("yandex_games", LoginMethodKind.YandexGames)]
        [TestCase("apple_game_center", LoginMethodKind.AppleGameCenter)]
        [TestCase("GooglePlay", LoginMethodKind.Unknown)]
        [TestCase("", LoginMethodKind.Unknown)]
        [TestCase(null, LoginMethodKind.Unknown)]
        public void Maps_every_wire_key_the_server_writes(string key, LoginMethodKind expected)
        {
            Assert.That(LoginMethodKinds.Parse(key), Is.EqualTo(expected));
        }

        [TestCase(LoginMethodKind.GooglePlay, true)]
        [TestCase(LoginMethodKind.VkGames, true)]
        [TestCase(LoginMethodKind.YandexGames, true)]
        [TestCase(LoginMethodKind.AppleGameCenter, true)]
        [TestCase(LoginMethodKind.Google, false)]
        [TestCase(LoginMethodKind.OpenId, false)]
        [TestCase(LoginMethodKind.Guest, false)]
        [TestCase(LoginMethodKind.Unknown, false)]
        public void Only_the_four_stores_sign_in_through_the_platform_call(LoginMethodKind kind, bool expected)
        {
            Assert.That(LoginMethodKinds.IsStore(kind), Is.EqualTo(expected));
        }
    }
}
