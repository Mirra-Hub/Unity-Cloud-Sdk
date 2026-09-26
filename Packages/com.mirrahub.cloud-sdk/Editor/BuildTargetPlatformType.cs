using System.Collections.Generic;
using MirraCloud.Editor.Dto;
using UnityEditor;

namespace MirraCloud.Editor
{
    /// <summary>
    /// Matches the active build target against the types of the project's platforms (console → Platforms: Web, PC,
    /// Mobile, Console), so the Manager window can flag a platform that was set up for other builds than this one.
    /// </summary>
    /// <remarks>
    /// The type names are the backend's <c>PlatformType</c> enum as the console API writes it. A build target that is
    /// none of the four — tvOS, visionOS, UWP, embedded Linux, a dedicated server — has no type and is not checked:
    /// any warning there would be a guess.
    /// </remarks>
    internal static class BuildTargetPlatformType
    {
        public const string Web = "Web";
        public const string Pc = "Pc";
        public const string Mobile = "Mobile";
        public const string Console = "Console";

        /// <summary>The platform type of the active build target, or null when it has none.</summary>
        public static string Current()
        {
            return ForTarget(EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.standaloneBuildSubtarget);
        }

        /// <summary>The platform type a build for <paramref name="target"/> runs on, or null when it has none.</summary>
        public static string ForTarget(BuildTarget target, StandaloneBuildSubtarget subtarget)
        {
            switch (BuildPipeline.GetBuildTargetGroup(target))
            {
                case BuildTargetGroup.Standalone:
                    // A dedicated server is not what players run, so there is no platform for it to match.
                    return subtarget == StandaloneBuildSubtarget.Server ? null : Pc;
                case BuildTargetGroup.WebGL:
                    return Web;
                case BuildTargetGroup.Android:
                case BuildTargetGroup.iOS:
                    return Mobile;
                case BuildTargetGroup.PS4:
                case BuildTargetGroup.PS5:
                case BuildTargetGroup.XboxOne:
                case BuildTargetGroup.GameCoreXboxOne:
                case BuildTargetGroup.GameCoreXboxSeries:
                case BuildTargetGroup.Switch:
                    return Console;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Whether a build of <paramref name="targetType"/> should not use this platform: the build has a type and the
        /// platform lists its types, none of them this one. A platform that lists no types says nothing either way.
        /// </summary>
        public static bool IsMismatch(EditorPlatformDto platform, string targetType)
        {
            if (targetType == null || platform?.platformTypes == null || platform.platformTypes.Count == 0) return false;
            return !platform.platformTypes.Contains(targetType);
        }

        /// <summary>
        /// The platform to start with when the configuration names none of the project's: the first switched-on one set
        /// up for <paramref name="targetType"/>, else the first switched-on one, else the first; -1 when there are none.
        /// </summary>
        public static int PickDefault(IReadOnlyList<EditorPlatformDto> platforms, string targetType)
        {
            var firstEnabled = -1;
            for (int i = 0; i < platforms.Count; i++)
            {
                var platform = platforms[i];
                if (!platform.isEnabled) continue;

                var isForTarget = targetType != null && platform.platformTypes != null &&
                                  platform.platformTypes.Contains(targetType);
                if (isForTarget) return i;
                if (firstEnabled < 0) firstEnabled = i;
            }

            if (firstEnabled >= 0) return firstEnabled;
            return platforms.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// A type as the console names it — Web, PC, Mobile, Console; one this version does not know, as it came.
        /// </summary>
        public static string DisplayName(string type)
        {
            return type == Pc ? "PC" : type;
        }

        /// <summary>The types of a platform for a label — "Web, PC" — or null when it lists none.</summary>
        public static string DisplayNames(List<string> types)
        {
            if (types == null || types.Count == 0) return null;

            var names = new string[types.Count];
            for (int i = 0; i < types.Count; i++)
            {
                names[i] = DisplayName(types[i]);
            }
            return string.Join(", ", names);
        }

        /// <summary>A build target as Build Settings names it: Windows rather than StandaloneWindows64.</summary>
        public static string TargetDisplayName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSX:
                    return "macOS";
                case BuildTarget.StandaloneLinux64:
                    return "Linux";
                case BuildTarget.GameCoreXboxOne:
                    return "Xbox One";
                case BuildTarget.GameCoreXboxSeries:
                    return "Xbox Series X|S";
                default:
                    return target.ToString();
            }
        }
    }
}
