namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>
    /// Which route a <c>Load*</c> call downloads through. The console decides which assets are
    /// public; this decides whether the call needs a signed-in player to reach them.
    /// </summary>
    public enum AssetAccess
    {
        /// <summary>
        /// As the signed-in player. Serves every asset of the branch, private or public. Without a
        /// player session the request is refused (HTTP 401).
        /// </summary>
        Player,

        /// <summary>
        /// Anonymously: no player session, no token. Serves only assets published in the console, on
        /// their own or through their folder (<see cref="Asset.IsEffectivelyPublic"/>); a private one is
        /// refused with HTTP 403, <c>assets_storage.asset_not_public</c>.
        /// </summary>
        Public,

        /// <summary>
        /// <see cref="Player"/> while there is a player session — signed in, or a saved one being
        /// restored — and <see cref="Public"/> otherwise. For code that runs both before and after
        /// sign-in: a public asset loads either way, a private one once the player is in.
        /// </summary>
        Auto,
    }
}
