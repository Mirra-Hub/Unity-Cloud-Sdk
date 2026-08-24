using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public class FileBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, FileBlobContainer> _openContainers = new Dictionary<string, FileBlobContainer>();

        protected virtual string RootPath
        {
            get { return Application.persistentDataPath; }
        }

        protected internal virtual string KeyExtension
        {
            get { return ".dat"; }
        }

        protected internal virtual Task CommitChangesAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task<IBlobContainer> OpenContainerAsync(string containerId)
        {
            if (_openContainers.TryGetValue(containerId, out FileBlobContainer container) == false)
            {
                container = new FileBlobContainer(this, containerId, Path.Combine(RootPath, containerId));
                _openContainers.Add(containerId, container);
            }

            container.AddRef();

            return Task.FromResult<IBlobContainer>(container);
        }

        public async Task DeleteContainerAsync(string containerId)
        {
            string containerPath = Path.Combine(RootPath, containerId);

            if (Directory.Exists(containerPath))
            {
                Directory.Delete(containerPath, true);
            }

            await CommitChangesAsync();
        }

        public virtual Task<IReadOnlyList<string>> ListContainersAsync(string prefix)
        {
            List<string> result = new List<string>();
            string rootPath = RootPath;

            if (Directory.Exists(rootPath))
            {
                string[] directories = Directory.GetDirectories(rootPath, prefix + "*");

                foreach (string directory in directories)
                {
                    result.Add(Path.GetFileName(directory));
                }
            }

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        internal void ReleaseContainer(FileBlobContainer container)
        {
            _openContainers.Remove(container.Id);
        }
    }
}
