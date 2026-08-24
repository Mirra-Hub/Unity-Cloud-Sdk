#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class IndexedDbBlobWriteBatch : IBlobWriteBatch
    {
        private readonly IndexedDbBlobContainer _container;
        private readonly List<KeyValuePair<string, byte[]>> _puts = new List<KeyValuePair<string, byte[]>>();
        private readonly List<string> _deletes = new List<string>();

        internal IndexedDbBlobWriteBatch(IndexedDbBlobContainer container)
        {
            _container = container;
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
            if (_puts.Count == 0 && _deletes.Count == 0)
            {
                return;
            }

            if (_container.IsReadOnly)
            {
                Debug.LogError($"[IndexedDbBlobWriteBatch] Container '{_container.Id}' is read-only (locked by another tab) — commit skipped");
                _puts.Clear();
                _deletes.Clear();
                return;
            }

            int batchId = IndexedDbNative.BeginBatch();

            foreach (KeyValuePair<string, byte[]> put in _puts)
            {
                IndexedDbNative.BatchPut(batchId, _container.BlobKey(put.Key), put.Value);
            }

            foreach (string key in _deletes)
            {
                IndexedDbNative.BatchDelete(batchId, _container.BlobKey(key));
            }

            bool ok = await IndexedDbNative.CommitBatchAsync(batchId, _container.Id);

            if (ok == false)
            {
                Debug.LogError($"[IndexedDbBlobWriteBatch] Commit failed for container '{_container.Id}' — data may not be persisted");
            }

            _puts.Clear();
            _deletes.Clear();
        }
    }
}
#endif
