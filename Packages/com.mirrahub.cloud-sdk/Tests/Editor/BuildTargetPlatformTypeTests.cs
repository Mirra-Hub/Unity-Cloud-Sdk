using System.Collections.Generic;
using MirraCloud.Editor.Dto;
using NUnit.Framework;
using UnityEditor;

namespace MirraCloud.Editor.Tests
{
    /// <summary>
    /// How the Manager window matches the build target against a platform's types (console → Platforms).
    ///
    /// <para>
    /// The check only warns, so the costly mistake is a wrong warning: a target that is none of the four types must
    /// stay unchecked, and a platform that lists no types must not be called a mismatch.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BuildTargetPlatformTypeTests
    {
        [TestCase(BuildTarget.StandaloneWindows, BuildTargetPlatformType.Pc)]
        [TestCase(BuildTarget.StandaloneWindows64, BuildTargetPlatformType.Pc)]
        [TestCase(BuildTarget.StandaloneOSX, BuildTargetPlatformType.Pc)]
        [TestCase(BuildTarget.StandaloneLinux64, BuildTargetPlatformType.Pc)]
        [TestCase(BuildTarget.WebGL, BuildTargetPlatformType.Web)]
        [TestCase(BuildTarget.Android, BuildTargetPlatformType.Mobile)]
        [TestCase(BuildTarget.iOS, BuildTargetPlatformType.Mobile)]
        [TestCase(BuildTarget.PS4, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.PS5, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.XboxOne, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.GameCoreXboxOne, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.GameCoreXboxSeries, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.Switch, BuildTargetPlatformType.Console)]
        [TestCase(BuildTarget.tvOS, null)]
        [TestCase(BuildTarget.WSAPlayer, null)]
        [TestCase(BuildTarget.EmbeddedLinux, null)]
        public void Maps_a_player_build_to_the_type_it_runs_on(BuildTarget target, string expected)
        {
            Assert.That(BuildTargetPlatformType.ForTarget(target, StandaloneBuildSubtarget.Player), Is.EqualTo(expected));
        }

        [TestCase(BuildTarget.StandaloneWindows64)]
        [TestCase(BuildTarget.StandaloneLinux64)]
        public void A_dedicated_server_has_no_type(BuildTarget target)
        {
            Assert.That(BuildTargetPlatformType.ForTarget(target, StandaloneBuildSubtarget.Server), Is.Null);
        }

        [Test]
        public void A_server_subtarget_left_from_a_standalone_build_does_not_hide_another_target()
        {
            Assert.That(
                BuildTargetPlatformType.ForTarget(BuildTarget.Android, StandaloneBuildSubtarget.Server),
                Is.EqualTo(BuildTargetPlatformType.Mobile));
        }

        [Test]
        public void A_platform_set_up_for_other_builds_is_a_mismatch()
        {
            var platform = Platform("web", BuildTargetPlatformType.Web);

            Assert.That(BuildTargetPlatformType.IsMismatch(platform, BuildTargetPlatformType.Mobile), Is.True);
        }

        [Test]
        public void A_platform_with_the_type_among_several_is_not_a_mismatch()
        {
            var platform = Platform("web-mobile", BuildTargetPlatformType.Web, BuildTargetPlatformType.Mobile);

            Assert.That(BuildTargetPlatformType.IsMismatch(platform, BuildTargetPlatformType.Mobile), Is.False);
        }

        [Test]
        public void A_target_without_a_type_is_never_a_mismatch()
        {
            var platform = Platform("web", BuildTargetPlatformType.Web);

            Assert.That(BuildTargetPlatformType.IsMismatch(platform, null), Is.False);
        }

        [Test]
        public void A_platform_that_lists_no_types_is_never_a_mismatch()
        {
            var withoutList = new EditorPlatformDto { key = "old", name = "Old", isEnabled = true };
            var withEmptyList = Platform("empty");

            Assert.That(BuildTargetPlatformType.IsMismatch(withoutList, BuildTargetPlatformType.Mobile), Is.False);
            Assert.That(BuildTargetPlatformType.IsMismatch(withEmptyList, BuildTargetPlatformType.Mobile), Is.False);
        }

        [Test]
        public void Starts_with_the_first_switched_on_platform_for_the_build_target()
        {
            var platforms = new List<EditorPlatformDto>
            {
                Platform("web", BuildTargetPlatformType.Web),
                Platform("android-old", false, BuildTargetPlatformType.Mobile),
                Platform("android", BuildTargetPlatformType.Mobile),
                Platform("ios", BuildTargetPlatformType.Mobile)
            };

            Assert.That(BuildTargetPlatformType.PickDefault(platforms, BuildTargetPlatformType.Mobile), Is.EqualTo(2));
        }

        [Test]
        public void Without_a_platform_for_the_build_target_starts_with_the_first_switched_on_one()
        {
            var platforms = new List<EditorPlatformDto>
            {
                Platform("web-old", false, BuildTargetPlatformType.Web),
                Platform("pc", BuildTargetPlatformType.Pc),
                Platform("web", BuildTargetPlatformType.Web)
            };

            Assert.That(BuildTargetPlatformType.PickDefault(platforms, BuildTargetPlatformType.Mobile), Is.EqualTo(1));
            Assert.That(BuildTargetPlatformType.PickDefault(platforms, null), Is.EqualTo(1));
        }

        [Test]
        public void With_every_platform_switched_off_starts_with_the_first()
        {
            var platforms = new List<EditorPlatformDto>
            {
                Platform("web", false, BuildTargetPlatformType.Web),
                Platform("android", false, BuildTargetPlatformType.Mobile)
            };

            Assert.That(BuildTargetPlatformType.PickDefault(platforms, BuildTargetPlatformType.Mobile), Is.EqualTo(0));
        }

        [Test]
        public void Without_platforms_picks_none()
        {
            var none = new List<EditorPlatformDto>();

            Assert.That(BuildTargetPlatformType.PickDefault(none, BuildTargetPlatformType.Web), Is.EqualTo(-1));
        }

        [Test]
        public void Names_the_types_as_the_console_does()
        {
            var types = new List<string> { "Web", "Pc", "Mobile", "Console", "Vr" };

            Assert.That(BuildTargetPlatformType.DisplayNames(types), Is.EqualTo("Web, PC, Mobile, Console, Vr"));
            Assert.That(BuildTargetPlatformType.DisplayNames(new List<string>()), Is.Null);
            Assert.That(BuildTargetPlatformType.DisplayNames(null), Is.Null);
        }

        [TestCase(BuildTarget.StandaloneWindows64, "Windows")]
        [TestCase(BuildTarget.StandaloneOSX, "macOS")]
        [TestCase(BuildTarget.StandaloneLinux64, "Linux")]
        [TestCase(BuildTarget.GameCoreXboxSeries, "Xbox Series X|S")]
        [TestCase(BuildTarget.Android, "Android")]
        [TestCase(BuildTarget.WebGL, "WebGL")]
        public void Names_the_build_target_as_Build_Settings_does(BuildTarget target, string expected)
        {
            Assert.That(BuildTargetPlatformType.TargetDisplayName(target), Is.EqualTo(expected));
        }

        private static EditorPlatformDto Platform(string key, params string[] types)
        {
            return Platform(key, true, types);
        }

        private static EditorPlatformDto Platform(string key, bool isEnabled, params string[] types)
        {
            return new EditorPlatformDto
            {
                key = key,
                name = key,
                isEnabled = isEnabled,
                platformTypes = new List<string>(types)
            };
        }
    }
}
