using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public class SqliteBlobStorage : IBlobStorage, IDisposable
    {
        private const string DB_EXTENSION = ".db";

        private readonly Dictionary<string, SqliteBlobContainer> _openContainers = new Dictionary<string, SqliteBlobContainer>();

        protected virtual string RootPath
        {
            get { return Application.persistentDataPath; }
        }

        public Task<IBlobContainer> OpenContainerAsync(string containerId)
        {
            if (_openContainers.TryGetValue(containerId, out SqliteBlobContainer container) == false)
            {
                container = new SqliteBlobContainer(this, containerId, GetDatabasePath(containerId));
                _openContainers.Add(containerId, container);
            }

            container.AddRef();

            return Task.FromResult<IBlobContainer>(container);
        }

        public async Task DeleteContainerAsync(string containerId)
        {
            if (_openContainers.TryGetValue(containerId, out SqliteBlobContainer container))
            {
                _openContainers.Remove(containerId);
                container.CloseConnection();
            }

            string databasePath = GetDatabasePath(containerId);

            await Task.Run(() =>
            {
                DeleteIfExists(databasePath);
                DeleteIfExists(databasePath + "-wal");
                DeleteIfExists(databasePath + "-shm");
            });
        }

        public async Task<IReadOnlyList<string>> ListContainersAsync(string prefix)
        {
            string rootPath = RootPath;

            return await Task.Run<IReadOnlyList<string>>(() =>
            {
                List<string> result = new List<string>();

                if (Directory.Exists(rootPath) == false)
                {
                    return result;
                }

                string[] files = Directory.GetFiles(rootPath, prefix + "*" + DB_EXTENSION);

                foreach (string file in files)
                {
                    result.Add(Path.GetFileNameWithoutExtension(file));
                }

                return result;
            });
        }

        public void Dispose()
        {
            foreach (SqliteBlobContainer container in _openContainers.Values)
            {
                container.CloseConnection();
            }

            _openContainers.Clear();
        }

        internal void ReleaseContainer(SqliteBlobContainer container)
        {
            _openContainers.Remove(container.Id);
            container.CloseConnection();
        }

        private string GetDatabasePath(string containerId)
        {
            return Path.Combine(RootPath, containerId + DB_EXTENSION);
        }

        private static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
