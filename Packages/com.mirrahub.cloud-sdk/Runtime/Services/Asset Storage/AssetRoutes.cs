namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>
    /// Every asset route the SDK calls, relative to the SDK base URL (<c>.../api/cloud/sdk</c>).
    /// </summary>
    /// <remarks>
    /// The player's route and the anonymous one differ only in the prefix. The anonymous one resolves to
    /// <c>.../api/cloud/sdk/public/assets/v1/...</c>, which the sdk-gateway routes without jwt-auth; the
    /// backend serves there only assets published in the console and answers 403 for the rest.
    /// </remarks>
    internal static class AssetRoutes
    {
        private const string PlayerApi = "/assets/v1";
        private const string PublicApi = "/public/assets/v1";

        public static string Config(string projectId, string branchId)
        {
            return $"{PlayerApi}/projects/{projectId}/branches/{branchId}/config";
        }

        public static string Download(string projectId, string branchId, bool anonymous, AssetAddress address)
        {
            string branch = $"{(anonymous ? PublicApi : PlayerApi)}/projects/{projectId}/branches/{branchId}";

            // By stable id rather than by the branch-local document id the server also accepts: the stable id
            // survives a re-import and is the same in every environment, so it is what a game hard-codes.
            return address.IsPath
                ? $"{branch}/path/{address.RoutePath}"
                : $"{branch}/assets/by-stable-id/{address.StableId}";
        }
    }
}
