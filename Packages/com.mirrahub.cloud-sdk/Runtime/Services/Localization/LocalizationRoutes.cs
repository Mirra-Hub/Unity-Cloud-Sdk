namespace MirraCloud.Core.Localization
{
    /// <summary>
    /// Every localization route the SDK calls, relative to the SDK base URL (<c>.../api/cloud/sdk</c>).
    /// The anonymous route differs only in the prefix: <c>.../api/cloud/sdk/public/localization/v1/...</c>
    /// is routed by the sdk-gateway without jwt-auth.
    /// </summary>
    internal static class LocalizationRoutes
    {
        private const string PlayerApi = "/localization/v1/projects";
        private const string PublicApi = "/public/localization/v1/projects";

        public static string Collection(string projectId, string branch, bool anonymous, string collectionKey)
        {
            return $"{(anonymous ? PublicApi : PlayerApi)}/{projectId}/branches/{branch}/collections/by-key/{Escape(collectionKey)}";
        }

        public static string KeyValues(string projectId, string branch, bool anonymous, string collectionKey, string key)
        {
            return $"{Collection(projectId, branch, anonymous, collectionKey)}/keys/{Escape(key)}/values";
        }

        public static string KeyValue(string projectId, string branch, bool anonymous, string collectionKey, string key, string language)
        {
            return $"{KeyValues(projectId, branch, anonymous, collectionKey, key)}/{language}";
        }

        public static string LegacyCollection(string projectId, string branch, string collectionId)
        {
            return $"{PlayerApi}/{projectId}/branches/{branch}/collections/{collectionId}";
        }

        private static string Escape(string segment)
        {
            return System.Uri.EscapeDataString(segment ?? string.Empty);
        }
    }
}
