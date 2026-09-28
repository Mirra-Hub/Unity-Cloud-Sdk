using System.Threading.Tasks;

namespace MirraCloud.Core.Storage
{
    /// <summary>
    /// String key-value storage for what the SDK keeps between launches: the guest id and the refresh token.
    /// <para>
    /// The stored values are loaded once, in the background, when the SDK starts: reads come from memory and see
    /// them only after <see cref="Ready"/>. A write lands in memory at once and reaches the disk right after, in
    /// order; <see cref="FlushAsync"/> waits for that.
    /// </para>
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Completes once the stored values are loaded. Never faults: a storage that cannot be read starts empty
        /// and keeps working in memory for the rest of the run.
        /// </summary>
        Task Ready { get; }

        bool HasKey(string key);

        /// <summary>The value, or null when the key has none.</summary>
        string GetString(string key);

        /// <summary>Saves the value. An empty or null value deletes the key.</summary>
        void SaveString(string key, string value);

        void DeleteKeys(params string[] keys);

        /// <summary>
        /// Completes once every write made before the call is on disk, or has failed and been logged. Never faults.
        /// </summary>
        Task FlushAsync();
    }
}
