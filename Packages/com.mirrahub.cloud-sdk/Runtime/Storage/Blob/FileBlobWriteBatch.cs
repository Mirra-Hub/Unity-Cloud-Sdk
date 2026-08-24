using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public class FileBlobWriteBatch : IBlobWriteBatch
    {
        private readonly FileBlobContainer _container;
        private readonly FileBlobStorage _storage;
        private readonly List<KeyValuePair<string, byte[]>> _puts = new List<KeyValuePair<string, byte[]>>();
        private readonly List<string> _deletes = new List<string>();

        internal FileBlobWriteBatch(FileBlobContainer container, FileBlobStorage storage)
        {
            _container = container;
            _storage = storage;
        }

        public void Put(string key, byte[] data)
        {
            _puts.Add(new KeyValuePair<string, byte[]>(key, data));
        }

        public void Delete(string key)
        {
            _deletes.Add(key);
        }

        public async Task CommitAsync()
        {
            foreach (KeyValuePair<string, byte[]> put in _puts)
            {
                _container.WriteFile(put.Key, put.Value);
            }

            foreach (string key in _deletes)
            {
                _container.DeleteFile(key);
            }

            _puts.Clear();
            _deletes.Clear();

            await _storage.CommitChangesAsync();
        }
    }
}
