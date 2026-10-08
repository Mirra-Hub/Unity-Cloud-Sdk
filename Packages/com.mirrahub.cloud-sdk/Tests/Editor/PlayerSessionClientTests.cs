using MirraCloud.Core.Auth;
using MirraCloud.Core.Auth.OpenId;
using NUnit.Framework;
using UnityEngine;

namespace MirraCloud.Core.Auth.Tests
{
    /// <summary>
    /// What the client sends about itself: the proof key of a browser sign-in, which the server checks byte for byte,
    /// and the client platform recorded on each session.
    /// </summary>
    public class PlayerSessionClientTests
    {
        [Test]
        public void The_challenge_is_base64url_sha256_of_the_verifier()
        {
            // RFC 7636, appendix B.
            Assert.AreEqual(
                "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
                OpenIdPkce.ChallengeOf("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
        }

        [Test]
        public void A_verifier_is_43_base64url_characters_and_never_repeats()
        {
            var first = OpenIdPkce.CreateVerifier();
            var second = OpenIdPkce.CreateVerifier();

            Assert.AreEqual(43, first.Length);
            StringAssert.IsMatch("^[A-Za-z0-9_-]{43}$", first);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(43, OpenIdPkce.ChallengeOf(first).Length);
        }

        [TestCase(RuntimePlatform.Android, ClientPlatform.Android)]
        [TestCase(RuntimePlatform.IPhonePlayer, ClientPlatform.Ios)]
        [TestCase(RuntimePlatform.WebGLPlayer, ClientPlatform.WebGl)]
        [TestCase(RuntimePlatform.WindowsPlayer, ClientPlatform.Windows)]
        [TestCase(RuntimePlatform.OSXPlayer, ClientPlatform.MacOs)]
        [TestCase(RuntimePlatform.LinuxPlayer, ClientPlatform.Linux)]
        [TestCase(RuntimePlatform.OSXEditor, ClientPlatform.Editor)]
        [TestCase(RuntimePlatform.WindowsEditor, ClientPlatform.Editor)]
        [TestCase(RuntimePlatform.PS5, ClientPlatform.Console)]
        [TestCase(RuntimePlatform.Switch, ClientPlatform.Console)]
        [TestCase(RuntimePlatform.tvOS, ClientPlatform.Unknown)]
        public void The_client_platform_follows_the_runtime_platform(RuntimePlatform runtime, ClientPlatform expected)
        {
            Assert.AreEqual(expected, DeviceInfoProvider.ToClientPlatform(runtime));
        }

        [Test]
        public void The_device_type_numbers_match_the_server()
        {
            Assert.AreEqual(ClientDeviceType.Handheld, DeviceInfoProvider.ToDeviceType(DeviceType.Handheld));
            Assert.AreEqual(ClientDeviceType.Desktop, DeviceInfoProvider.ToDeviceType(DeviceType.Desktop));
            Assert.AreEqual(ClientDeviceType.Console, DeviceInfoProvider.ToDeviceType(DeviceType.Console));
            Assert.AreEqual(ClientDeviceType.Unknown, DeviceInfoProvider.ToDeviceType(DeviceType.Unknown));
            Assert.AreEqual(3, (int)ClientDeviceType.Desktop);
            Assert.AreEqual(8, (int)ClientPlatform.Console);
        }

        [Test]
        public void The_device_carries_the_sdk_version()
        {
            Assert.AreEqual(MirraCloudSdkInfo.Version, DeviceInfoProvider.Collect().SdkVersion);
        }
    }
}
