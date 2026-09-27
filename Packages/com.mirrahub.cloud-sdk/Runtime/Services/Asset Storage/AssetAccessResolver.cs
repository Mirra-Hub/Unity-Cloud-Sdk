namespace MirraCloud.Core.AssetsStorage
{
    internal static class AssetAccessResolver
    {
        /// <summary>Whether a load with <paramref name="access"/> goes through the anonymous route.</summary>
        /// <param name="hasSession">A signed-in player, or a saved session the SDK is still restoring. The
        /// latter counts: a request sent during the restore is refused once, waits for the restore, and
        /// goes out again with the new token — while the anonymous route would refuse a private asset
        /// for good.</param>
        public static bool IsAnonymous(AssetAccess access, bool hasSession)
        {
            switch (access)
            {
                case AssetAccess.Public:
                    return true;
                case AssetAccess.Auto:
                    return hasSession == false;
                default:
                    return false;
            }
        }
    }
}
