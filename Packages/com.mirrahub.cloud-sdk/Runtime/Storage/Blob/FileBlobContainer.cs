using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public class FileBlobContainer : IBlobContainer
    {
        private readonly FileBlobStorage _storage;
        private readonly string _containerPath;
        private int _refCount;

        public string Id { get; }

        internal FileBlobContainer(FileBlobStorage storage, string id, string containerPath)
        {
            _storage = storage;
            Id = id;
            _containerPath = containerPath;
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

        public Task<bool> TryAcquireExclusiveAsync()
        {
            return Task.FromResult(true);
        }

        public Task<BlobResult> ReadAsync(string key)
        {
            string filePath = GetFilePath(key);

            if (File.Exists(filePath) == false)
            {
                return Task.FromResult(BlobResult.NotFound());
            }

            try
            {
                return Task.FromResult(BlobResult.Ok(File.ReadAllBytes(filePath)));
            }
            catch (IOException)
            {
                return Task.FromResult(BlobResult.Error());
            }
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(File.Exists(GetFilePath(key)));
        }

        public Task ReadManyAsync(IReadOnlyList<string> keys, Action<string, byte[]> onBlob)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                string filePath = GetFilePath(keys[i]);

                if (File.Exists(filePath))
                {
                    onBlob(keys[i], File.ReadAllBytes(filePath));
                }
            }

            return Task.CompletedTask;
        }

        public Task ReadByPrefixAsync(string keyPrefix, Action<string, byte[]> onBlob)
        {
            foreach (string filePath in EnumerateFilesByPrefix(keyPrefix))
            {
                onBlob(GetKeyFromFilePath(filePath), File.ReadAllBytes(filePath));
            }

            return Task.CompletedTask;
        }

        public IBlobWriteBatch BeginWrite()
        {
            return new FileBlobWriteBatch(this, _storage);
        }

        public async Task DeleteByPrefixAsync(string keyPrefix)
        {
            List<string> filePaths = new List<string>(EnumerateFilesByPrefix(keyPrefix));

            foreach (string filePath in filePaths)
            {
                File.Delete(filePath);
            }

            await _storage.CommitChangesAsync();
        }

        internal void WriteFile(string key, byte[] data)
        {
            string filePath = GetFilePath(key);
            string directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, data);
        }

        internal void DeleteFile(string key)
        {
            string filePath = GetFilePath(key);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_containerPath, key + _storage.KeyExtension);
        }

        private string GetKeyFromFilePath(string filePath)
        {
            string key = filePath.Substring(_containerPath.Length).TrimStart('/', '\\').Replace('\\', '/');
            string extension = _storage.KeyExtension;

            if (extension.Length > 0 && key.EndsWith(extension, StringComparison.Ordinal))
            {
                key = key.Substring(0, key.Length - extension.Length);
            }

            return key;
        }

        private IEnumerable<string> EnumerateFilesByPrefix(string keyPrefix)
        {
            if (Directory.Exists(_containerPath) == false)
            {
                yield break;
            }

            string pattern = "*" + _storage.KeyExtension;

            foreach (string filePath in Directory.EnumerateFiles(_containerPath, pattern, SearchOption.AllDirectories))
            {
                string key = GetKeyFromFilePath(filePath);

                if (key.StartsWith(keyPrefix, StringComparison.Ordinal))
                {
                    yield return filePath;
                }
            }
        }
    }
}
