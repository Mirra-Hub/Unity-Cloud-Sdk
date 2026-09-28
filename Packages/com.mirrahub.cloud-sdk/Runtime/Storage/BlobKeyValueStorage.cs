using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MirraCloud.Core.Logger;
using MirraCloud.Core.Storage.Blob;

namespace MirraCloud.Core.Storage
{
    /// <summary>
    /// <see cref="IStorage"/> over one container of the blob storage — SQLite, or IndexedDB in WebGL — within the
    /// area of one project.
    /// <para>
    /// Everything the area holds is read into memory once, as the storage is created; that read is
    /// <see cref="Ready"/>, and after it reads never wait. A write changes memory at once and is committed right
    /// away. Writes made while a commit runs wait for it and go out together in the next one, the latest value of
    /// each key winning, so the disk sees them in order and every batch lands whole or not at all.
    /// </para>
    /// <para>
    /// The area is <c>{scope}/</c>: in WebGL all the games of one site share a single IndexedDB database, and
    /// without it one game would read another's session — and, when the server refused it, delete it.
    /// </para>
    /// <para>
    /// It never takes <see cref="IBlobContainer.TryAcquireExclusiveAsync"/>: a second browser tab would turn
    /// read-only and could not keep the refresh token the server hands it.
    /// </para>
    /// </summary>
    internal sealed class BlobKeyValueStorage : IStorage, IDisposable
    {
        private readonly IBlobStorage _blobStorage;
        private readonly string _containerId;
        private readonly string _prefix;
        private readonly ILogger _logger;

        // The SDK calls in on the main thread, but the blob backends complete off it (and the tests run on the
        // thread pool), so every field below is read and written under this lock.
        private readonly object _sync = new object();
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        // Key → value to write, or null to delete, until a commit takes it.
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>();
        // Keys written before the stored values arrived: loading must not bring back what they replaced.
        private readonly HashSet<string> _writtenBeforeLoad = new HashSet<string>();
        private readonly List<TaskCompletionSource<bool>> _flushWaiters = new List<TaskCompletionSource<bool>>();

        private IBlobContainer _container;
        private bool _loaded;
        private bool _committing;
        private bool _disposed;

        public Task Ready { get; }

        /// <param name="scope">The project id: every key lives under <c>{scope}/</c>. Empty becomes <c>_</c>.</param>
        public BlobKeyValueStorage(IBlobStorage blobStorage, string containerId, string scope, ILogger logger)
        {
            _blobStorage = blobStorage;
            _containerId = containerId;
            _prefix = (string.IsNullOrEmpty(scope) ? "_" : scope) + "/";
            _logger = logger;

            Ready = LoadAsync();
        }

        public bool HasKey(string key)
        {
            lock (_sync)
            {
                return _values.ContainsKey(key);
            }
        }

        public string GetString(string key)
        {
            lock (_sync)
            {
                return _values.TryGetValue(key, out string value) ? value : null;
            }
        }

        public void SaveString(string key, string value)
        {
            // An empty value would be a zero-length blob, and SQLite keeps the data column NOT NULL: a key with
            // nothing in it is a deleted key.
            if (string.IsNullOrEmpty(value))
            {
                DeleteKeys(key);
                return;
            }

            lock (_sync)
            {
                _values[key] = value;
                Stage(key, value);
            }

            ScheduleCommit();
        }

        public void DeleteKeys(params string[] keys)
        {
            lock (_sync)
            {
                foreach (string key in keys)
                {
                    _values.Remove(key);
                    Stage(key, null);
                }
            }

            ScheduleCommit();
        }

        public Task FlushAsync()
        {
            lock (_sync)
            {
                if (_committing == false && _pending.Count == 0)
                {
                    return _loaded ? Task.CompletedTask : Ready;
                }

                var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _flushWaiters.Add(waiter);
                return waiter.Task;
            }
        }

        /// <summary>
        /// Lets go of the container without waiting: a commit still running closes it when it is done. Blocking
        /// here could hang the main thread — the SQLite backend finishes its work on it.
        /// </summary>
        public void Dispose()
        {
            bool closeNow;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                closeNow = _committing == false;
            }

            if (closeNow)
            {
                CloseContainer();
            }
        }

        private async Task LoadAsync()
        {
            var stored = new List<KeyValuePair<string, byte[]>>();

            try
            {
                IBlobContainer container = await _blobStorage.OpenContainerAsync(_containerId);

                bool disposed;
                lock (_sync)
                {
                    disposed = _disposed;
                    if (disposed == false)
                    {
                        _container = container;
                    }
                }

                if (disposed)
                {
                    container.Dispose();
                }
                else
                {
                    await container.ReadByPrefixAsync(_prefix, (key, data) =>
                    {
                        lock (stored)
                        {
                            stored.Add(new KeyValuePair<string, byte[]>(key, data));
                        }
                    });
                }
            }
            catch (Exception exception)
            {
                // No native SQLite on this platform, a damaged file: the SDK still works, it just will not
                // remember the player between launches.
                _logger.Error($"Mirra Cloud: local storage '{_containerId}' could not be read, so the sign-in " +
                              $"will not be kept between launches. {exception.Message}");
            }

            lock (_sync)
            {
                foreach (KeyValuePair<string, byte[]> entry in stored)
                {
                    string key = entry.Key.Substring(_prefix.Length);

                    if (_writtenBeforeLoad.Contains(key) == false)
                    {
                        _values[key] = Encoding.UTF8.GetString(entry.Value);
                    }
                }

                _writtenBeforeLoad.Clear();
                _loaded = true;
            }

            ScheduleCommit();
        }

        private void Stage(string key, string value)
        {
            _pending[key] = value;

            if (_loaded == false)
            {
                _writtenBeforeLoad.Add(key);
            }
        }

        private void ScheduleCommit()
        {
            lock (_sync)
            {
                if (_loaded == false || _committing || _pending.Count == 0)
                {
                    return;
                }

                _committing = true;
            }

            _ = CommitAsync();
        }

        private async Task CommitAsync()
        {
            TaskCompletionSource<bool>[] waiters;
            bool close;

            while (true)
            {
                KeyValuePair<string, string>[] batch;
                IBlobContainer container;

                lock (_sync)
                {
                    if (_pending.Count == 0)
                    {
                        // Stop and collect the waiters in one step: a write coming in right after starts a commit
                        // of its own, and a flush that waits for it must not be answered by this one.
                        _committing = false;
                        waiters = _flushWaiters.ToArray();
                        _flushWaiters.Clear();
                        close = _disposed;
                        break;
                    }

                    batch = new KeyValuePair<string, string>[_pending.Count];
                    ((ICollection<KeyValuePair<string, string>>)_pending).CopyTo(batch, 0);
                    _pending.Clear();
                    container = _container;
                }

                if (container == null)
                {
                    // The container never opened (already logged) or is closed: there is nowhere to write.
                    continue;
                }

                try
                {
                    IBlobWriteBatch write = container.BeginWrite();

                    foreach (KeyValuePair<string, string> item in batch)
                    {
                        if (item.Value == null)
                        {
                            write.Delete(_prefix + item.Key);
                        }
                        else
                        {
                            write.Put(_prefix + item.Key, Encoding.UTF8.GetBytes(item.Value));
                        }
                    }

                    await write.CommitAsync();
                }
                catch (Exception exception)
                {
                    _logger.Error($"Mirra Cloud: writing to local storage '{_containerId}' failed. {exception.Message}");
                }
            }

            foreach (TaskCompletionSource<bool> waiter in waiters)
            {
                waiter.TrySetResult(true);
            }

            if (close)
            {
                CloseContainer();
            }
        }

        private void CloseContainer()
        {
            IBlobContainer container;

            lock (_sync)
            {
                container = _container;
                _container = null;
            }

            container?.Dispose();
        }
    }
}
