using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    /// <summary>
    /// The listing behind the editor's cache window: every key with the size of its value, without the values.
    /// </summary>
    [TestFixture]
    public class SqliteBlobListingTests
    {
        private string _rootPath;
        private SqliteBlobStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"mirracloud_bloblisting_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootPath);
            _storage = new TempSqliteBlobStorage(_rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            _storage.Dispose();

            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        [UnityTest]
        public IEnumerator Lists_every_key_with_its_size_in_key_order()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("asset_cache");

                try
                {
                    await container.WriteAsync("p/main/b/v1", new byte[10]);
                    await container.WriteAsync("p/main/a/v2", new byte[300]);
                    await container.WriteAsync("p/main/c/v1", new byte[0]);

                    IReadOnlyList<KeyValuePair<string, long>> entries = await ((SqliteBlobContainer)container).ListEntriesAsync();

                    Assert.That(entries, Is.EqualTo(new[]
                    {
                        new KeyValuePair<string, long>("p/main/a/v2", 300),
                        new KeyValuePair<string, long>("p/main/b/v1", 10),
                        new KeyValuePair<string, long>("p/main/c/v1", 0),
                    }));
                }
                finally
                {
                    container.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator An_empty_container_lists_nothing()
        {
            return TaskCoroutine.Run(async () =>
            {
                IBlobContainer container = await _storage.OpenContainerAsync("asset_cache");

                try
                {
                    Assert.That(await ((SqliteBlobContainer)container).ListEntriesAsync(), Is.Empty);
                }
                finally
                {
                    container.Dispose();
                }
            });
        }
    }
}
