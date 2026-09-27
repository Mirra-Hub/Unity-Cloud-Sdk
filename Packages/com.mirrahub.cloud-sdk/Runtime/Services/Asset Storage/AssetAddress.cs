namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>How a load names its asset on the server: by stable id, or by path.</summary>
    internal readonly struct AssetAddress
    {
        public readonly string StableId;

        /// <summary>The path in stored form, <c>/icons/coin.png</c>; null for an address by id.</summary>
        public readonly string StoredPath;

        /// <summary>The same path escaped for the URL, segment by segment.</summary>
        public readonly string RoutePath;

        private AssetAddress(string stableId, string storedPath, string routePath)
        {
            StableId = stableId;
            StoredPath = storedPath;
            RoutePath = routePath;
        }

        public bool IsPath => StoredPath != null;

        public static AssetAddress ById(string stableId)
        {
            return new AssetAddress(stableId, null, null);
        }

        public static AssetAddress ByPath(string storedPath, string routePath)
        {
            return new AssetAddress(null, storedPath, routePath);
        }

        /// <summary>What a log line or an error message calls the asset.</summary>
        public override string ToString()
        {
            return IsPath ? StoredPath : StableId;
        }
    }
}
