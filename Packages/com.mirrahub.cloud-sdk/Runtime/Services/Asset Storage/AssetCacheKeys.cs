namespace MirraCloud.Core.AssetsStorage
{
    internal static class AssetCacheKeys
    {
        public static string Asset(string assetKey, int version)
        {
            return $"{assetKey}/v{version}";
        }

        // Everything stored for one asset, whatever the version — what a version bump prunes.
        public static string Prefix(string assetKey)
        {
            return $"{assetKey}/";
        }

        /// <summary>
        /// Reads back a key the service wrote: <c>{project}/{branch}/{stableId}/v{version}</c>. A branch name may
        /// hold <c>/</c>, so the branch is everything between the first segment and the last two. False for a key
        /// of any other shape.
        /// </summary>
        public static bool TryParse(string key, out string projectId, out string branch, out string stableId, out int version)
        {
            projectId = null;
            branch = null;
            stableId = null;
            version = 0;

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            string[] parts = key.Split('/');

            if (parts.Length < 4)
            {
                return false;
            }

            string versionPart = parts[parts.Length - 1];

            if (versionPart.Length < 2 || versionPart[0] != 'v' || int.TryParse(versionPart.Substring(1), out version) == false)
            {
                version = 0;
                return false;
            }

            projectId = parts[0];
            branch = string.Join("/", parts, 1, parts.Length - 3);
            stableId = parts[parts.Length - 2];

            return true;
        }
    }
}
