#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class IndexedDbBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, IndexedDbBlobContainer> _openContainers = new Dictionary<string, IndexedDbBlobContainer>();

        public Task<IBlobContainer> OpenContainerAsync(string containerId)
        {
            if (_openContainers.TryGetValue(containerId, out IndexedDbBlobContainer container) == false)
            {
                container = new IndexedDbBlobContainer(this, containerId);
                _openContainers.Add(containerId, container);
            }

            container.AddRef();

            return Task.FromResult<IBlobContainer>(container);
        }

        public async Task DeleteContainerAsync(string containerId)
        {
            bool ok = await IndexedDbNative.DeleteContainerAsync(containerId);

            if (ok == false)
            {
                UnityEngine.Debug.LogError($"[IndexedDbBlobStorage] Failed to delete container: {containerId}");
            }
        }

        public async Task<IReadOnlyList<string>> ListContainersAsync(string prefix)
        {
            return await IndexedDbNative.ListContainersAsync(prefix);
        }

        internal void ReleaseContainer(IndexedDbBlobContainer container)
        {
            _openContainers.Remove(container.Id);
        }
    }
}
#endif
