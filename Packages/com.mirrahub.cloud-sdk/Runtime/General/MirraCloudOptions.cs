namespace MirraCloud
{
    /// <summary>
    /// Settings the game sets from code, over the ones in <c>Configuration.asset</c> (MirraCloud → Manager). Pass them
    /// to <see cref="Core.IMirraCloudSdk.Initialize(MirraCloudOptions)"/>. A field left null, empty or blank is taken
    /// from the asset; a set one wins over it and is trimmed. The asset itself is not changed.
    /// </summary>
    /// <example>
    /// <code>
    /// sdk.Initialize(new MirraCloudOptions { Branch = "qa", PlatformKey = "yandex_games" });
    /// </code>
    /// </example>
    public sealed class MirraCloudOptions
    {
        /// <summary>The project's id, as the console shows it.</summary>
        public string ProjectId;

        /// <summary>The branch NAME, such as <c>main</c> — the one the console shows.</summary>
        public string Branch;

        /// <summary>The project's API token.</summary>
        public string Token;

        /// <summary>
        /// Key of the platform this build runs on (console → Platforms). Decides which sign-in methods the player gets
        /// and which platform analytics counts the events under. Case-sensitive.
        /// </summary>
        public string PlatformKey;
    }
}
