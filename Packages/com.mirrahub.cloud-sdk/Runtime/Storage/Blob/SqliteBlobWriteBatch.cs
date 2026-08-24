using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class SqliteBlobWriteBatch : IBlobWriteBatch
    {
        private readonly SqliteBlobContainer _container;
        private readonly List<KeyValuePair<string, byte[]>> _puts = new List<KeyValuePair<string, byte[]>>();
        private readonly List<string> _deletes = new List<string>();

        internal SqliteBlobWriteBatch(SqliteBlobContainer container)
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

            try
            {
                await _container.CommitBatchAsync(_puts, _deletes);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SqliteBlobWriteBatch] Commit failed for container '{_container.Id}' — data may not be persisted: {exception.Message}");
            }

            _puts.Clear();
            _deletes.Clear();
        }
    }
}
