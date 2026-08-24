using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public static class BlobStorageExtensions
    {
        public static async Task WriteAsync(this IBlobContainer container, string key, byte[] data)
        {
            IBlobWriteBatch batch = container.BeginWrite();
            batch.Put(key, data);
            await batch.CommitAsync();
        }

        public static async Task DeleteAsync(this IBlobContainer container, string key)
        {
            IBlobWriteBatch batch = container.BeginWrite();
            batch.Delete(key);
            await batch.CommitAsync();
        }

        public static async Task<List<KeyValuePair<string, byte[]>>> ReadAllByPrefixAsync(this IBlobContainer container, string keyPrefix)
        {
            List<KeyValuePair<string, byte[]>> result = new List<KeyValuePair<string, byte[]>>();

            await container.ReadByPrefixAsync(keyPrefix, (key, data) => result.Add(new KeyValuePair<string, byte[]>(key, data)));

            return result;
        }
    }
}
