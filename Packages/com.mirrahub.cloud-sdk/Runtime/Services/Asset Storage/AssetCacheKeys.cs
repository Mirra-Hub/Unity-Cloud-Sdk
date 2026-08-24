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
    }
}
