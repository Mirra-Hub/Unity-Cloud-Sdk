namespace MirraCloud.Core.Storage
{
    /// <summary>
    /// Containers of the SDK's own data in the blob storage. The <c>mirracloud_</c> prefix keeps them apart from
    /// the game's files in <c>persistentDataPath</c>.
    /// </summary>
    internal static class LocalDataContainers
    {
        /// <summary>The guest id and the refresh token, in a build.</summary>
        public const string PrefsInBuilds = "mirracloud_prefs";

        /// <summary>
        /// The same in the editor. The editor shares <c>persistentDataPath</c> with a standalone build of the same
        /// project; with one container the two would be one player, and chats or friends could not be tried with
        /// the editor and a build side by side — PlayerPrefs, which kept this before, were separate for them.
        /// </summary>
        public const string PrefsInEditor = "mirracloud_prefs_editor";

#if UNITY_EDITOR
        public const string Prefs = PrefsInEditor;
#else
        public const string Prefs = PrefsInBuilds;
#endif
    }
}
