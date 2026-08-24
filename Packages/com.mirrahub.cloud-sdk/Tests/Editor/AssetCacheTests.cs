using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using MirraCloud.Core.AssetsStorage;
using MirraCloud.Core.Storage.Blob;
using UnityEngine.TestTools;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    [TestFixture("file")]
    [TestFixture("sqlite")]
    public class AssetCacheTests
    {
        private readonly string _backend;

        private string _rootPath;
        private IBlobStorage _storage;

        public AssetCacheTests(string backend)
        {
            _backend = backend;
        }

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"mirracloud_assetcache_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootPath);
            _storage = CreateStorage(_rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (_storage is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        [UnityTest]
        public IEnumerator Miss_DownloadsOnce_ThenServesFromCache()
        {
            return TaskCoroutine.Run(async () =>
            {
                int calls = 0;
                byte[] payload = Bytes(1, 128);
                Func<Task<DownloadedAsset<byte[]>>> download = () => { calls++; return Task.FromResult(new DownloadedAsset<byte[]>(payload, payload)); };
                var cache = new AssetCache(_storage, "asset_cache");

                try
                {
                    byte[] first = await cache.GetOrLoadAsync("tex_a", 1, Identity, download);
                    byte[] second = await cache.GetOrLoadAsync("tex_a", 1, Identity, download);

                    Assert.AreEqual(1, calls);
                    Assert.AreEqual(payload, first);
                    Assert.AreEqual(payload, second);
                }
                finally
                {
                    cache.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator VersionBump_ReDownloads()
        {
            return TaskCoroutine.Run(async () =>
            {
                int calls = 0;
                byte[] v1 = Bytes(1, 64);
                byte[] v2 = Bytes(2, 64);
                Func<Task<DownloadedAsset<byte[]>>> download = () =>
                {
                    calls++;
                    byte[] value = calls == 1 ? v1 : v2;
                    return Task.FromResult(new DownloadedAsset<byte[]>(value, value));
                };
                var cache = new AssetCache(_storage, "asset_cache");

                try
                {
                    byte[] r1 = await cache.GetOrLoadAsync("tex_a", 1, Identity, download);
                    byte[] r2 = await cache.GetOrLoadAsync("tex_a", 2, Identity, download);

                    Assert.AreEqual(2, calls);
                    Assert.AreEqual(v1, r1);
                    Assert.AreEqual(v2, r2);
                }
                finally
                {
                    cache.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator OldVersion_IsPrunedOnBump()
        {
            return TaskCoroutine.Run(async () =>
            {
                int calls = 0;
                Func<Task<DownloadedAsset<byte[]>>> download = () =>
                {
                    calls++;
                    byte[] value = Bytes(calls, 32);
                    return Task.FromResult(new DownloadedAsset<byte[]>(value, value));
                };
                var cache = new AssetCache(_storage, "asset_cache");

                try
                {
                    await cache.GetOrLoadAsync("tex_a", 1, Identity, download);   // download #1 (v1)
                    await cache.GetOrLoadAsync("tex_a", 2, Identity, download);   // download #2 (v2), prunes v1
                    await cache.GetOrLoadAsync("tex_a", 1, Identity, download);   // v1 gone -> download #3

                    Assert.AreEqual(3, calls);
                }
                finally
                {
                    cache.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator DownloadFailure_ReturnsNull_AndDoesNotCache()
        {
            return TaskCoroutine.Run(async () =>
            {
                int calls = 0;
                Func<Task<DownloadedAsset<byte[]>>> download = () => { calls++; return Task.FromResult(default(DownloadedAsset<byte[]>)); };
                var cache = new AssetCache(_storage, "asset_cache");

                try
                {
                    byte[] first = await cache.GetOrLoadAsync("tex_a", 1, Identity, download);
                    byte[] second = await cache.GetOrLoadAsync("tex_a", 1, Identity, download);

                    Assert.IsNull(first);
                    Assert.IsNull(second);
                    Assert.AreEqual(2, calls);
                }
                finally
                {
                    cache.Dispose();
                }
            });
        }

        private static Task<byte[]> Identity(byte[] bytes)
        {
            return Task.FromResult(bytes);
        }

        private IBlobStorage CreateStorage(string rootPath)
        {
            if (string.Equals(_backend, "sqlite"))
            {
                return new TempSqliteBlobStorage(rootPath);
            }

            return new TempFileBlobStorage(rootPath);
        }

        private static byte[] Bytes(int seed, int size)
        {
            byte[] data = new byte[size];

            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(seed * 31 + i * 7);
            }

            return data;
        }
    }
}
