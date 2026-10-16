namespace MirraCloud.Core.Localization
{
    /// <summary>
    /// Which route a localization read goes through. The console decides which collections are public;
    /// this decides whether the call needs a signed-in player to reach them.
    /// </summary>
    public enum LocalizationAccess
    {
        /// <summary>
        /// As the signed-in player. Reads every collection of the branch, private or public. Without a
        /// player session the request is refused (HTTP 401).
        /// </summary>
        Player,

        /// <summary>
        /// Anonymously: no player session, no token. Reads only collections published as public in the
        /// console; a private one is refused with HTTP 403, <c>game_localization.collection_not_public</c>.
        /// </summary>
        Public,

        /// <summary>
        /// <see cref="Player"/> while there is a player session — signed in, or a saved one being restored —
        /// and <see cref="Public"/> otherwise. For text shown both before and after sign-in, such as the
        /// sign-in screen itself.
        /// </summary>
        Auto,
    }
}
