#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class BlobStorageWebSmokeTest : ICloudSdkService
    {
        private readonly IBlobStorage _storage;

        private int _failures;

        public BlobStorageWebSmokeTest(IBlobStorage storage)
        {
            _storage = storage;
        }

        public void CloudSdkInitialize()
        {
            if (Application.absoluteURL.Contains("blobtest=1"))
            {
                _ = RunAsync();
            }
        }

        public void CloudSdkDispose()
        {
        }

        private async Task RunAsync()
        {
            string containerId = $"smoketest_{DateTime.UtcNow.Ticks}";
            Debug.Log($"[BlobSmokeTest] Started, container: {containerId}");

            try
            {
                IBlobContainer container = await _storage.OpenContainerAsync(containerId);
                Check(await container.TryAcquireExclusiveAsync(), "exclusive lock acquired");

                byte[] payload = CreateBlob(1, 4096);

                await container.WriteAsync("places/earth/regions/r.0.0_blocks", payload);
                BlobResult read = await container.ReadAsync("places/earth/regions/r.0.0_blocks");
                Check(read.Status == BlobStatus.Success && BytesEqual(read.Value, payload), "put/read round-trip");

                BlobResult missing = await container.ReadAsync("players/player_missing");
                Check(missing.Status == BlobStatus.NotFound, "missing key -> NotFound");

                IBlobWriteBatch batch = container.BeginWrite();
                batch.Put("players/player_a", CreateBlob(2, 128));
                batch.Put("players/player_b", CreateBlob(3, 128));
                await batch.CommitAsync();

                Check(await container.ExistsAsync("players/player_a"), "batch put a");
                Check(await container.ExistsAsync("players/player_b"), "batch put b");

                List<string> prefixKeys = new List<string>();
                await container.ReadByPrefixAsync("players/", (key, data) => prefixKeys.Add(key));
                Check(prefixKeys.Count == 2, $"prefix read exact count (got {prefixKeys.Count})");

                List<string> manyKeys = new List<string> { "players/player_a", "players/player_missing", "players/player_b" };
                int manyFound = 0;
                await container.ReadManyAsync(manyKeys, (key, data) => manyFound++);
                Check(manyFound == 2, $"multi-get exact count (got {manyFound})");

                await container.DeleteByPrefixAsync("players/");
                Check(await container.ExistsAsync("players/player_a") == false, "prefix delete");
                Check(await container.ExistsAsync("places/earth/regions/r.0.0_blocks"), "prefix delete keeps others");

                IReadOnlyList<string> listed = await _storage.ListContainersAsync("smoketest_");
                Check(Contains(listed, containerId), "container listed");

                container.Dispose();
                await _storage.DeleteContainerAsync(containerId);

                listed = await _storage.ListContainersAsync("smoketest_");
                Check(Contains(listed, containerId) == false, "container deleted");
            }
            catch (Exception exception)
            {
                _failures++;
                Debug.LogError($"[BlobSmokeTest] Exception: {exception}");
            }

            if (_failures == 0)
            {
                Debug.Log("[BlobSmokeTest] PASSED — all checks ok");
            }
            else
            {
                Debug.LogError($"[BlobSmokeTest] FAILED — {_failures} check(s) failed");
            }
        }

        private void Check(bool condition, string label)
        {
            if (condition)
            {
                Debug.Log($"[BlobSmokeTest] ok: {label}");
            }
            else
            {
                _failures++;
                Debug.LogError($"[BlobSmokeTest] FAIL: {label}");
            }
        }

        private static bool Contains(IReadOnlyList<string> items, string value)
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

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
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
#endif
