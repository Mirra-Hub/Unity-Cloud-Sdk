using System;
using System.Threading.Tasks;
using MirraCloud.Core.Storage.Blob;

namespace MirraCloud.Core.AssetsStorage
{
    public sealed class AssetCache : IDisposable
    {
        private readonly IBlobStorage _storage;
        private readonly string _containerId;

        private Task<IBlobContainer> _openTask;
        private IBlobContainer _container;

        public AssetCache(IBlobStorage storage, string containerId)
        {
            _storage = storage;
            _containerId = containerId;
        }

        /// <param name="assetKey">Identifies the asset across every project and branch this game
        /// talks to — the container is shared, and one entry per key is kept, the current version.</param>
        public async Task<T> GetOrLoadAsync<T>(string assetKey, int version, Func<byte[], Task<T>> reconstructFromBytes, Func<Task<DownloadedAsset<T>>> download)
        {
            IBlobContainer cache = await GetContainerAsync();

            BlobResult hit = await cache.ReadAsync(AssetCacheKeys.Asset(assetKey, version));

            if (hit.Success)
            {
                return await reconstructFromBytes(hit.Value);
            }

            DownloadedAsset<T> downloaded = await download();

            if (downloaded.Bytes != null)
            {
                await cache.DeleteByPrefixAsync(AssetCacheKeys.Prefix(assetKey));
                await cache.WriteAsync(AssetCacheKeys.Asset(assetKey, version), downloaded.Bytes);
            }

            return downloaded.Value;
        }

        private Task<IBlobContainer> GetContainerAsync()
        {
            if (_openTask == null)
            {
                _openTask = OpenAsync();
            }

            return _openTask;
        }

        private async Task<IBlobContainer> OpenAsync()
        {
            _container = await _storage.OpenContainerAsync(_containerId);
            return _container;
        }

        public void Dispose()
        {
            if (_container != null)
            {
                _container.Dispose();
                _container = null;
            }
        }
    }
}
