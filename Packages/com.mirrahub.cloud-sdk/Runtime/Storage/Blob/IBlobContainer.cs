using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public interface IBlobContainer : IDisposable
    {
        string Id { get; }

        Task<bool> TryAcquireExclusiveAsync();

        Task<BlobResult> ReadAsync(string key);

        Task<bool> ExistsAsync(string key);

        Task ReadManyAsync(IReadOnlyList<string> keys, Action<string, byte[]> onBlob);

        Task ReadByPrefixAsync(string keyPrefix, Action<string, byte[]> onBlob);

        IBlobWriteBatch BeginWrite();

        Task DeleteByPrefixAsync(string keyPrefix);
    }
}
