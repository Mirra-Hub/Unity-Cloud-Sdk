using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using MirraCloud.Core.Storage.Blob;
using UnityEngine.TestTools;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    [TestFixture("file")]
    [TestFixture("sqlite")]
    public class BlobStorageRoundTripTests
    {
        private readonly string _backend;

        private string _rootPath;
        private IBlobStorage _storage;

        public BlobStorageRoundTripTests(string backend)
        {
            _backend = backend;
        }

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"mirracloud_blobtest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootPath);
            _storage = CreateStorage(_rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            DisposeStorage(_storage);

            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        [UnityTest]
        public IEnumerator PutRead_RoundTripsBytes()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                byte[] data = CreateBlob(1, 4096);
                await container.WriteAsync("places/earth_surface/regions/r.0.0_blocks", data);

                BlobResult result = await container.ReadAsync("places/earth_surface/regions/r.0.0_blocks");

                Assert.AreEqual(BlobStatus.Success, result.Status);
                Assert.AreEqual(data, result.Value);
            });
        }

        [UnityTest]
        public IEnumerator Read_MissingKey_ReturnsNotFound()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                BlobResult result = await container.ReadAsync("players/player_missing");

                Assert.AreEqual(BlobStatus.NotFound, result.Status);
            });
        }

        [UnityTest]
        public IEnumerator Exists_ReflectsPutAndDelete()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                Assert.IsFalse(await container.ExistsAsync("universe"));

                await container.WriteAsync("universe", CreateBlob(2, 128));
                Assert.IsTrue(await container.ExistsAsync("universe"));

                await container.DeleteAsync("universe");
                Assert.IsFalse(await container.ExistsAsync("universe"));
            });
        }

        [UnityTest]
        public IEnumerator Batch_CommitsPutsAndDeletesTogether()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                await container.WriteAsync("players/player_a", CreateBlob(3, 64));

                IBlobWriteBatch batch = container.BeginWrite();
                batch.Put("players/player_b", CreateBlob(4, 64));
                batch.Put("players/player_c", CreateBlob(5, 64));
                batch.Delete("players/player_a");
                await batch.CommitAsync();

                Assert.IsFalse(await container.ExistsAsync("players/player_a"));
                Assert.IsTrue(await container.ExistsAsync("players/player_b"));
                Assert.IsTrue(await container.ExistsAsync("players/player_c"));
            });
        }

        [UnityTest]
        public IEnumerator ReadByPrefix_ReturnsExactlyMatchingKeys()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                await container.WriteAsync("places/earth_surface/regions/r.0.0_blocks", CreateBlob(6, 64));
                await container.WriteAsync("places/earth_surface/regions/r.0.1_blocks", CreateBlob(7, 64));
                await container.WriteAsync("places/earth_surface/place", CreateBlob(8, 64));
                await container.WriteAsync("players/player_a", CreateBlob(9, 64));

                List<KeyValuePair<string, byte[]>> found = await container.ReadAllByPrefixAsync("places/earth_surface/regions/");

                Assert.AreEqual(2, found.Count);

                HashSet<string> keys = new HashSet<string>();
                foreach (KeyValuePair<string, byte[]> pair in found)
                {
                    keys.Add(pair.Key);
                }

                Assert.IsTrue(keys.Contains("places/earth_surface/regions/r.0.0_blocks"));
                Assert.IsTrue(keys.Contains("places/earth_surface/regions/r.0.1_blocks"));
            });
        }

        [UnityTest]
        public IEnumerator DeleteByPrefix_RemovesOnlyMatchingKeys()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                await container.WriteAsync("places/earth_surface/regions/r.0.0_blocks", CreateBlob(10, 64));
                await container.WriteAsync("places/earth_surface/regions/r.0.0_entities", CreateBlob(11, 64));
                await container.WriteAsync("places/earth_surface/place", CreateBlob(12, 64));

                await container.DeleteByPrefixAsync("places/earth_surface/regions/");

                Assert.IsFalse(await container.ExistsAsync("places/earth_surface/regions/r.0.0_blocks"));
                Assert.IsFalse(await container.ExistsAsync("places/earth_surface/regions/r.0.0_entities"));
                Assert.IsTrue(await container.ExistsAsync("places/earth_surface/place"));
            });
        }

        [UnityTest]
        public IEnumerator ListContainers_FiltersByPrefix_AndDeleteContainerRemoves()
        {
            return TaskCoroutine.Run(async () =>
            {
                await OpenAndWrite("universe_alpha");
                await OpenAndWrite("universe_beta");
                await OpenAndWrite("asset_cache");

                IReadOnlyList<string> universes = await _storage.ListContainersAsync("universe_");

                Assert.AreEqual(2, universes.Count);
                Assert.IsTrue(ContainsItem(universes, "universe_alpha"));
                Assert.IsTrue(ContainsItem(universes, "universe_beta"));

                await _storage.DeleteContainerAsync("universe_alpha");

                universes = await _storage.ListContainersAsync("universe_");

                Assert.AreEqual(1, universes.Count);
                Assert.IsTrue(ContainsItem(universes, "universe_beta"));
            });
        }

        [UnityTest]
        public IEnumerator Commit_IsDurableAcrossStorageInstances()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");
                byte[] data = CreateBlob(13, 2048);

                await container.WriteAsync("universe", data);

                IBlobStorage reopened = CreateStorage(_rootPath);

                try
                {
                    IBlobContainer reopenedContainer = await reopened.OpenContainerAsync("universe_test");
                    BlobResult result = await reopenedContainer.ReadAsync("universe");

                    Assert.AreEqual(BlobStatus.Success, result.Status);
                    Assert.AreEqual(data, result.Value);
                }
                finally
                {
                    DisposeStorage(reopened);
                }
            });
        }

        [UnityTest]
        public IEnumerator OpenContainer_ReturnsSharedInstance()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer first = await _storage.OpenContainerAsync("universe_test");
                IBlobContainer second = await _storage.OpenContainerAsync("universe_test");

                Assert.AreSame(first, second);
            });
        }

        [UnityTest]
        public IEnumerator ReadMany_ReturnsOnlyExistingKeys()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                byte[] first = CreateBlob(20, 128);
                byte[] second = CreateBlob(21, 256);

                await container.WriteAsync("chunks/c.0.0/blocks", first);
                await container.WriteAsync("chunks/c.0.1/blocks", second);

                List<string> keys = new List<string>
                {
                    "chunks/c.0.0/blocks",
                    "chunks/c.0.1/blocks",
                    "chunks/c.9.9/blocks",
                };

                Dictionary<string, byte[]> found = new Dictionary<string, byte[]>();
                await container.ReadManyAsync(keys, (key, data) => found[key] = data);

                Assert.AreEqual(2, found.Count);
                Assert.AreEqual(first, found["chunks/c.0.0/blocks"]);
                Assert.AreEqual(second, found["chunks/c.0.1/blocks"]);
            });
        }

        [UnityTest]
        public IEnumerator TryAcquireExclusive_ReturnsTrue()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("universe_test");

                Assert.IsTrue(await container.TryAcquireExclusiveAsync());
            });
        }

        private IBlobStorage CreateStorage(string rootPath)
        {
            if (string.Equals(_backend, "sqlite"))
            {
                return new TempSqliteBlobStorage(rootPath);
            }

            return new TempFileBlobStorage(rootPath);
        }

        private static void DisposeStorage(IBlobStorage storage)
        {
            if (storage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private async Task OpenAndWrite(string containerId)
        {
            IBlobContainer container = await _storage.OpenContainerAsync(containerId);
            await container.WriteAsync("universe", CreateBlob(0, 32));
        }

        private static bool ContainsItem(IReadOnlyList<string> items, string value)
        {
            foreach (string item in items)
            {
                if (string.Equals(item, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] CreateBlob(int seed, int size)
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
