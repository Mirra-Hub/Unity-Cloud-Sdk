using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MirraCloud.Core.Storage.Blob;
using MirraCloud.Core.Storage.Blob.Tests;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MirraCloud.Core.Storage.Tests
{
    /// <summary>
    /// The SDK keeps the player's sign-in here. What it writes has to be there on the next launch, in the order it
    /// was written, and only for the project that wrote it — on every backend the SDK runs on.
    /// </summary>
    [TestFixture("file")]
    [TestFixture("sqlite")]
    public class BlobKeyValueStorageTests
    {
        private const string Container = "prefs_test";

        private readonly string _backend;
        private readonly List<IDisposable> _opened = new List<IDisposable>();
        private string _rootPath;

        public BlobKeyValueStorageTests(string backend)
        {
            _backend = backend;
        }

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"mirracloud_prefstest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            // Key-value storages first, then the blob storages under them.
            for (int i = _opened.Count - 1; i >= 0; i--)
            {
                _opened[i].Dispose();
            }

            _opened.Clear();

            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        [UnityTest]
        public IEnumerator A_written_value_is_there_after_a_restart()
        {
            return TaskCoroutine.Run(async () =>
            {
                BlobKeyValueStorage storage = await OpenAsync();

                storage.SaveString("auth/refresh_token", "token-1");
                Assert.That(storage.GetString("auth/refresh_token"), Is.EqualTo("token-1"), "reads come from memory at once");
                await storage.FlushAsync();

                BlobKeyValueStorage restarted = await OpenAsync();
                Assert.That(restarted.HasKey("auth/refresh_token"), Is.True);
                Assert.That(restarted.GetString("auth/refresh_token"), Is.EqualTo("token-1"));
            });
        }

        [UnityTest]
        public IEnumerator A_deleted_key_stays_deleted_after_a_restart()
        {
            return TaskCoroutine.Run(async () =>
            {
                BlobKeyValueStorage storage = await OpenAsync();
                storage.SaveString("auth/refresh_token", "token-1");
                await storage.FlushAsync();

                storage.DeleteKeys("auth/refresh_token");
                Assert.That(storage.HasKey("auth/refresh_token"), Is.False);
                await storage.FlushAsync();

                BlobKeyValueStorage restarted = await OpenAsync();
                Assert.That(restarted.HasKey("auth/refresh_token"), Is.False);
                Assert.That(restarted.GetString("auth/refresh_token"), Is.Null);
            });
        }

        [UnityTest]
        public IEnumerator Writes_made_back_to_back_reach_the_disk_in_order()
        {
            return TaskCoroutine.Run(async () =>
            {
                BlobKeyValueStorage storage = await OpenAsync();

                // No awaits in between: the later writes pile up behind the first commit.
                storage.SaveString("a", "1");
                storage.SaveString("a", "2");
                storage.SaveString("b", "x");
                storage.DeleteKeys("b");
                storage.SaveString("c", "3");
                await storage.FlushAsync();

                BlobKeyValueStorage restarted = await OpenAsync();
                Assert.That(restarted.GetString("a"), Is.EqualTo("2"));
                Assert.That(restarted.HasKey("b"), Is.False);
                Assert.That(restarted.GetString("c"), Is.EqualTo("3"));
            });
        }

        [UnityTest]
        public IEnumerator A_write_made_while_loading_is_not_undone_by_the_load()
        {
            return TaskCoroutine.Run(async () =>
            {
                BlobKeyValueStorage first = await OpenAsync();
                first.SaveString("auth/guest_id", "old");
                await first.FlushAsync();

                // SQLite reads on the thread pool, so this write lands before the stored "old" does.
                BlobKeyValueStorage second = Open();
                second.SaveString("auth/guest_id", "new");
                await second.Ready;
                Assert.That(second.GetString("auth/guest_id"), Is.EqualTo("new"));
                await second.FlushAsync();

                BlobKeyValueStorage restarted = await OpenAsync();
                Assert.That(restarted.GetString("auth/guest_id"), Is.EqualTo("new"));
            });
        }

        [UnityTest]
        public IEnumerator Each_project_sees_only_its_own_keys()
        {
            return TaskCoroutine.Run(async () =>
            {
                // One blob storage, as in a WebGL page where every game of the site shares one IndexedDB database.
                IBlobStorage shared = NewBlobStorage();
                BlobKeyValueStorage gameA = Open("project_a", shared);
                BlobKeyValueStorage gameB = Open("project_b", shared);
                await gameA.Ready;
                await gameB.Ready;

                gameA.SaveString("auth/refresh_token", "token-a");
                gameB.SaveString("auth/refresh_token", "token-b");
                await gameA.FlushAsync();
                await gameB.FlushAsync();

                gameB.DeleteKeys("auth/refresh_token");
                await gameB.FlushAsync();

                BlobKeyValueStorage restartedA = await OpenAsync("project_a");
                BlobKeyValueStorage restartedB = await OpenAsync("project_b");
                Assert.That(restartedA.GetString("auth/refresh_token"), Is.EqualTo("token-a"));
                Assert.That(restartedB.HasKey("auth/refresh_token"), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator An_empty_value_deletes_the_key_and_any_text_round_trips()
        {
            return TaskCoroutine.Run(async () =>
            {
                const string text = "Привет, мир — 🎮 \"quotes\" / slashes\\ \n new line";
                BlobKeyValueStorage storage = await OpenAsync();

                storage.SaveString("a", "1");
                storage.SaveString("a", "");
                storage.SaveString("b", null);
                storage.SaveString("text", text);
                Assert.That(storage.HasKey("a"), Is.False);
                Assert.That(storage.HasKey("b"), Is.False);
                await storage.FlushAsync();

                BlobKeyValueStorage restarted = await OpenAsync();
                Assert.That(restarted.HasKey("a"), Is.False);
                Assert.That(restarted.GetString("text"), Is.EqualTo(text));
            });
        }

        [UnityTest]
        public IEnumerator A_flush_with_nothing_written_waits_only_for_the_load()
        {
            return TaskCoroutine.Run(async () =>
            {
                BlobKeyValueStorage storage = Open();
                await storage.FlushAsync();
                Assert.That(storage.Ready.IsCompleted, Is.True);
            });
        }

        private IBlobStorage NewBlobStorage()
        {
            IBlobStorage storage = _backend == "sqlite"
                ? new TempSqliteBlobStorage(_rootPath)
                : (IBlobStorage)new TempFileBlobStorage(_rootPath);

            if (storage is IDisposable disposable)
            {
                _opened.Add(disposable);
            }

            return storage;
        }

        private BlobKeyValueStorage Open(string scope = "project_a", IBlobStorage blobStorage = null)
        {
            var storage = new BlobKeyValueStorage(blobStorage ?? NewBlobStorage(), Container, scope, new RecordingLogger());
            _opened.Add(storage);
            return storage;
        }

        /// <summary>A storage as the next launch would see it: new instances over the same files.</summary>
        private async Task<BlobKeyValueStorage> OpenAsync(string scope = "project_a")
        {
            BlobKeyValueStorage storage = Open(scope);
            await storage.Ready;
            return storage;
        }
    }

    /// <summary>A backend that cannot be opened — no native SQLite on the platform, a damaged file.</summary>
    [TestFixture]
    public class BlobKeyValueStorageFailureTests
    {
        [UnityTest]
        public IEnumerator A_storage_that_cannot_open_keeps_working_in_memory()
        {
            return TaskCoroutine.Run(async () =>
            {
                var logger = new RecordingLogger();
                var storage = new BlobKeyValueStorage(new UnopenableBlobStorage(), "prefs_test", "project_a", logger);

                await storage.Ready;
                Assert.That(storage.Ready.IsFaulted, Is.False);
                Assert.That(logger.Errors, Has.Count.EqualTo(1));

                storage.SaveString("auth/guest_id", "guest-1");
                Assert.That(storage.GetString("auth/guest_id"), Is.EqualTo("guest-1"));
                await storage.FlushAsync();

                storage.Dispose();
            });
        }

        private sealed class UnopenableBlobStorage : IBlobStorage
        {
            public Task<IBlobContainer> OpenContainerAsync(string containerId)
            {
                throw new DllNotFoundException("sqlite3");
            }

            public Task DeleteContainerAsync(string containerId)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<string>> ListContainersAsync(string prefix)
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
        }
    }

    /// <summary>
    /// SQLite runs its queries on the thread pool under a gate, and closing the connection waits for that gate on
    /// the calling thread — the main one, when the SDK is disposed. The gate used to be let go by a continuation
    /// queued for the main thread, so disposing during a write hung the editor. It runs on the main thread here on
    /// purpose: the thread pool has no queue to hang on.
    /// </summary>
    [TestFixture]
    public class SqliteDisposeDuringCommitTests
    {
        [UnityTest]
        public IEnumerator Disposing_in_the_middle_of_a_commit_does_not_wait_for_the_main_thread()
        {
            if (SynchronizationContext.Current == null)
            {
                Assert.Inconclusive("Needs the editor's main-thread context.");
            }

            string root = Path.Combine(Path.GetTempPath(), $"mirracloud_disposetest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var blobStorage = new TempSqliteBlobStorage(root);
                var storage = new BlobKeyValueStorage(blobStorage, "prefs_test", "project_a", new RecordingLogger());

                while (storage.Ready.IsCompleted == false)
                {
                    yield return null;
                }

                // The commit takes the gate on this thread and runs the transaction on the pool.
                storage.SaveString("auth/refresh_token", "token-1");
                storage.Dispose();

                // Closing waits for the gate. Doing it off this thread lets this thread give up after a while
                // instead of hanging with it, which is what a release queued for the main thread would do.
                Task dispose = Task.Run(() => blobStorage.Dispose());
                Assert.That(dispose.Wait(TimeSpan.FromSeconds(10)), Is.True,
                    "closing SQLite waited for the main thread while a commit held the gate");

                Task flushed = storage.FlushAsync();
                for (int frame = 0; frame < 300 && flushed.IsCompleted == false; frame++)
                {
                    yield return null;
                }

                var reopenedBlobStorage = new TempSqliteBlobStorage(root);
                var reopened = new BlobKeyValueStorage(reopenedBlobStorage, "prefs_test", "project_a", new RecordingLogger());

                while (reopened.Ready.IsCompleted == false)
                {
                    yield return null;
                }

                Assert.That(reopened.GetString("auth/refresh_token"), Is.EqualTo("token-1"));

                reopened.Dispose();
                reopenedBlobStorage.Dispose();
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }

    internal sealed class RecordingLogger : Logger.ILogger
    {
        private readonly object _sync = new object();

        public List<string> Errors { get; } = new List<string>();

        public void Log(string message)
        {
        }

        public void Error(string message)
        {
            lock (_sync)
            {
                Errors.Add(message);
            }
        }
    }
}
