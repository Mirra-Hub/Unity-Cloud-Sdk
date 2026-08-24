#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class IndexedDbBlobContainer : IBlobContainer
    {
        private readonly IndexedDbBlobStorage _storage;
        private bool _lockChecked;
        private int _refCount;

        public string Id { get; }

        public bool IsReadOnly { get; private set; }

        internal IndexedDbBlobContainer(IndexedDbBlobStorage storage, string id)
        {
            _storage = storage;
            Id = id;
        }

        internal void AddRef()
        {
            _refCount++;
        }

        public void Dispose()
        {
            _refCount--;

            if (_refCount <= 0)
            {
                _storage.ReleaseContainer(this);
            }
        }

        public async Task<bool> TryAcquireExclusiveAsync()
        {
            if (_lockChecked)
            {
                return IsReadOnly == false;
            }

            _lockChecked = true;
            bool held = await IndexedDbNative.AcquireContainerLockAsync(Id);

            if (held == false)
            {
                IsReadOnly = true;
                Debug.LogWarning($"[IndexedDbBlobContainer] Container '{Id}' is locked by another tab — opened read-only");
            }

            return IsReadOnly == false;
        }

        public Task<BlobResult> ReadAsync(string key)
        {
            return IndexedDbNative.GetAsync(BlobKey(key));
        }

        public Task<bool> ExistsAsync(string key)
        {
            return IndexedDbNative.ExistsAsync(BlobKey(key));
        }

        public async Task ReadManyAsync(IReadOnlyList<string> keys, Action<string, byte[]> onBlob)
        {
            if (keys.Count == 0)
            {
                return;
            }

            string prefix = Id + "/";
            string[] blobKeys = new string[keys.Count];

            for (int i = 0; i < keys.Count; i++)
            {
                blobKeys[i] = prefix + keys[i];
            }

            await IndexedDbNative.GetManyAsync(blobKeys, (fullKey, data) =>
            {
                onBlob(fullKey.Substring(prefix.Length), data);
            });
        }

        public async Task ReadByPrefixAsync(string keyPrefix, Action<string, byte[]> onBlob)
        {
            string prefix = Id + "/";

            await IndexedDbNative.ReadPrefixAsync(prefix + keyPrefix, (fullKey, data) =>
            {
                onBlob(fullKey.Substring(prefix.Length), data);
            });
        }

        public IBlobWriteBatch BeginWrite()
        {
            return new IndexedDbBlobWriteBatch(this);
        }

        public async Task DeleteByPrefixAsync(string keyPrefix)
        {
            bool ok = await IndexedDbNative.DeletePrefixAsync(Id + "/" + keyPrefix);

            if (ok == false)
            {
                Debug.LogError($"[IndexedDbBlobContainer] DeleteByPrefix failed for '{keyPrefix}' in container '{Id}'");
            }
        }

        internal string BlobKey(string key)
        {
            return Id + "/" + key;
        }
    }
}
#endif
