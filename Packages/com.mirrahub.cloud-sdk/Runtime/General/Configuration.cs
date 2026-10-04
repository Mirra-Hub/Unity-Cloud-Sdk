using UnityEngine;
using UnityEngine.Serialization;

namespace MirraCloud
{
    [CreateAssetMenu(menuName = "Mirra Cloud/Create Configuration", fileName = "Configuration", order = 0)]
    public class Configuration : ScriptableObject
    {
        // The Mirra Cloud domain. Both hosts are served directly by the ingress.
        //
        // Each name is a different gateway, so they are not interchangeable. The editor url is the
        // service-account gateway, NOT api.mirracloud.com: that one is the client api-gateway,
        // which declares none of the editor routes and drops them into its /api/cloud/** catch-all,
        // where a policy demanding a Cloud client role answers 401 before any backend is reached.
        private const string PROD_SDK_URL = "https://sdk.mirracloud.com/api/cloud/sdk";
        private const string PROD_EDITOR_URL = "https://sa.mirracloud.com";

        private const string RESOURCES_PATH = "Configuration";

        [Header("General")]
        public string ProjectId;
        /// <summary>
        /// Branch reference used in API routes (<c>branches/{branch}</c>). Holds the branch NAME;
        /// the server resolves a branch by name.
        /// </summary>
        /// <remarks>Was <c>BranchId</c>; assets saved before the rename keep their value.</remarks>
        [FormerlySerializedAs("BranchId")]
        public string Branch;
        public string Token;

        /// <summary>
        /// Key of the platform this build runs on, as set in the console (Platforms). Sign-in sends it in the
        /// <c>PlatformKey</c> header and analytics puts it in the request path, so it decides which sign-in
        /// methods the player gets and which platform the events are counted under. Case-sensitive.
        /// </summary>
        /// <remarks>
        /// Replaces <c>AnalyticsPlatformId</c> on purpose without <c>[FormerlySerializedAs]</c>: that field held the
        /// platform's internal id, which no route accepts any more, so carrying it over would only turn every sign-in
        /// into a <c>platforms.platform_unknown</c> refusal.
        /// </remarks>
        [Tooltip("Key of the platform this build runs on (Cloud console → Platforms). Sent with every sign-in and analytics request.")]
        public string PlatformKey;

        public string Url { get; private set; }
        public string EditorApiUrl { get; private set; }

        /// <summary>
        /// <see cref="PlatformKey"/> without surrounding whitespace, or null when it is not set. The sign-in header
        /// is trimmed by the server while the analytics path is taken as is, so both read this one value.
        /// </summary>
        internal string ResolvedPlatformKey => string.IsNullOrWhiteSpace(PlatformKey) ? null : PlatformKey.Trim();

        /// <summary>
        /// Points <see cref="Url"/> and <see cref="EditorApiUrl"/> at production, or at the profile
        /// the project's DeveloperSettings asset selects. Both are runtime-only, so this has to run
        /// on every load — <see cref="Load"/> does it for you; call it yourself if you got hold of
        /// the asset some other way (the editor tooling does).
        /// </summary>
        public void ResolveEnvironment()
        {
            Url = PROD_SDK_URL;
            EditorApiUrl = PROD_EDITOR_URL;

            var devSettings = DeveloperSettings.TryLoad();
            var profile = devSettings != null ? devSettings.ActiveProfile : null;
            if (profile != null)
            {
                if (string.IsNullOrEmpty(profile.SdkUrl) == false)
                {
                    Url = profile.SdkUrl;
                }

                if (string.IsNullOrEmpty(profile.EditorUrl) == false)
                {
                    EditorApiUrl = profile.EditorUrl;
                }
            }
        }


        /// <summary>
        /// Loads the project's configuration from any <c>Resources</c> folder. The asset lives in the
        /// project, not in the SDK itself — the Manager window creates it under
        /// <c>Assets/MirraCloud/Resources</c> the first time you connect.
        /// </summary>
        /// <remarks>
        /// Returns the asset itself, so it does not show what the game set in <see cref="MirraCloudOptions"/>.
        /// </remarks>
        public static Configuration Load()
        {
            Configuration configuration = Resources.Load<Configuration>(RESOURCES_PATH);

            if (configuration == null)
            {
                Debug.LogError(
                    "Mirra Cloud: Configuration.asset not found in any Resources folder. Open " +
                    "MirraCloud > Manager and connect the project — the asset is created " +
                    "for you. Requests will fail until then.");

                configuration = CreateInstance<Configuration>();
            }

            configuration.ResolveEnvironment();
            return configuration;
        }

        /// <summary>
        /// What the SDK runs with: a copy of the asset with <paramref name="options"/> applied over it. A copy, since
        /// options applied to the asset itself would stay in it for the rest of the editor session, and the Manager
        /// window would write them to disk on its next save.
        /// </summary>
        internal static Configuration Load(MirraCloudOptions options)
        {
            Configuration asset = Resources.Load<Configuration>(RESOURCES_PATH);

            Configuration configuration = asset != null ? Instantiate(asset) : CreateInstance<Configuration>();
            configuration.ApplyOverrides(options);

            if (asset == null && configuration.HasProjectAndBranch == false)
            {
                Debug.LogError(
                    "Mirra Cloud: Configuration.asset not found in any Resources folder. Open " +
                    "MirraCloud > Manager and connect the project — the asset is created for you — or pass " +
                    "ProjectId and Branch in MirraCloudOptions. Requests will fail until then.");
            }

            configuration.ResolveEnvironment();
            return configuration;
        }

        /// <summary>
        /// Overwrites each field <paramref name="options"/> sets — not null, empty or blank — with its trimmed value.
        /// </summary>
        internal void ApplyOverrides(MirraCloudOptions options)
        {
            if (options == null)
            {
                return;
            }

            ProjectId = Override(ProjectId, options.ProjectId);
            Branch = Override(Branch, options.Branch);
            Token = Override(Token, options.Token);
            PlatformKey = Override(PlatformKey, options.PlatformKey);
        }

        private bool HasProjectAndBranch =>
            string.IsNullOrWhiteSpace(ProjectId) == false && string.IsNullOrWhiteSpace(Branch) == false;

        private static string Override(string current, string fromCode)
        {
            return string.IsNullOrWhiteSpace(fromCode) ? current : fromCode.Trim();
        }
    }
}
