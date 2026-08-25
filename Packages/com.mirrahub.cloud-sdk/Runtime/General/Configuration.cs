using UnityEngine;

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
        public string BranchId;
        public string Token;
        public string AnalyticsPlatformId;

        public string Url { get; private set; }
        public string EditorApiUrl { get; private set; }

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
        public static Configuration Load()
        {
            Configuration configuration = Resources.Load<Configuration>(RESOURCES_PATH);

            if (configuration == null)
            {
                Debug.LogError(
                    "Mirra Cloud: Configuration.asset not found in any Resources folder. Open " +
                    "Tools > Mirra Cloud > Manager and connect the project — the asset is created " +
                    "for you. Requests will fail until then.");

                configuration = CreateInstance<Configuration>();
            }

            configuration.ResolveEnvironment();
            return configuration;
        }
    }
}