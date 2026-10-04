using UnityEditor;

namespace MirraCloud.Editor
{
    /// <summary>
    /// MirraCloud → Data: what the SDK keeps on this machine between play sessions.
    /// <list type="bullet">
    /// <item><b>Clear Sign-In</b> forgets the session and the guest id the editor keeps, so the next guest sign-in
    /// makes a new player. Edit → Clear All PlayerPrefs did this while the SDK kept them in PlayerPrefs; it no longer
    /// does. Builds keep a container of their own, which this leaves alone.</item>
    /// <item><b>Clear Cache</b> deletes the downloaded assets. The cache is shared with a standalone build of the same
    /// project on this machine, so that build downloads again too.</item>
    /// <item><b>Show Cache</b> lists what the cache holds.</item>
    /// </list>
    /// Both Clear items are off in Play Mode: the running SDK holds the data open.
    /// </summary>
    internal static class LocalDataMenu
    {
        [MenuItem(MirraCloudMenu.ClearSignIn, false, MirraCloudMenu.ClearSignInPriority)]
        private static void ClearSignIn()
        {
            if (EditorLocalData.RefuseWhilePlaying())
            {
                return;
            }

            _ = EditorLocalData.ClearSignInAsync();
        }

        [MenuItem(MirraCloudMenu.ClearSignIn, true)]
        private static bool CanClearSignIn()
        {
            return EditorLocalData.IsPlaying == false;
        }

        [MenuItem(MirraCloudMenu.ClearCache, false, MirraCloudMenu.ClearCachePriority)]
        private static void ClearCache()
        {
            if (EditorLocalData.RefuseWhilePlaying())
            {
                return;
            }

            _ = EditorLocalData.ClearAssetCacheAsync();
        }

        [MenuItem(MirraCloudMenu.ClearCache, true)]
        private static bool CanClearCache()
        {
            return EditorLocalData.IsPlaying == false;
        }

        [MenuItem(MirraCloudMenu.ShowCache, false, MirraCloudMenu.ShowCachePriority)]
        private static void ShowCache()
        {
            AssetCacheWindow.Open();
        }
    }
}
