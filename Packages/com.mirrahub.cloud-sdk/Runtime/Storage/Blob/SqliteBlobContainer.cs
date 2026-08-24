using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

namespace MirraCloud.Core.Storage.Blob
{
    public sealed class SqliteBlobContainer : IBlobContainer
    {
        private const char RANGE_END_SUFFIX = char.MaxValue;

        private readonly SqliteBlobStorage _storage;
        private readonly string _databasePath;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private SQLiteConnection _connection;
        private int _refCount;

        public string Id { get; }

        internal SqliteBlobContainer(SqliteBlobStorage storage, string id, string databasePath)
        {
            _storage = storage;
            Id = id;
            _databasePath = databasePath;
        }

        internal void AddRef()
        {
            _refCount++;
        }

        public void Dispose()
        {
            _refCount--;

            if (_refCount <= 0)
            {
                _storage.ReleaseContainer(this);
            }
        }

        public Task<bool> TryAcquireExclusiveAsync()
        {
            return Task.FromResult(true);
        }

        public async Task<BlobResult> ReadAsync(string key)
        {
            await _gate.WaitAsync();

            try
            {
                return await Task.Run(() =>
                {
                    List<byte[]> rows = GetConnection().QueryScalars<byte[]>("SELECT data FROM blobs WHERE key = ?", key);
                    return rows.Count > 0 ? BlobResult.Ok(rows[0]) : BlobResult.NotFound();
                });
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SqliteBlobContainer] Read failed for '{key}' in '{Id}': {exception.Message}");
                return BlobResult.Error();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            await _gate.WaitAsync();

            try
            {
                return await Task.Run(() =>
                {
                    List<int> rows = GetConnection().QueryScalars<int>("SELECT 1 FROM blobs WHERE key = ? LIMIT 1", key);
                    return rows.Count > 0;
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ReadManyAsync(IReadOnlyList<string> keys, Action<string, byte[]> onBlob)
        {
            if (keys.Count == 0)
            {
                return;
            }

            List<string> foundKeys = new List<string>(keys.Count);
            List<byte[]> foundValues = new List<byte[]>(keys.Count);

            await _gate.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    SQLiteConnection connection = GetConnection();

                    using (SQLitePreparedStatement select = new SQLitePreparedStatement(connection, "SELECT data FROM blobs WHERE key = ?"))
                    {
                        for (int i = 0; i < keys.Count; i++)
                        {
                            select.Bind(1, keys[i]);

                            if (select.Step() == SQLite3.Result.Row)
                            {
                                foundKeys.Add(keys[i]);
                                foundValues.Add(select.GetBytes(0));
                            }

                            select.Reset();
                        }
                    }
                });
            }
            finally
            {
                _gate.Release();
            }

            for (int i = 0; i < foundKeys.Count; i++)
            {
                onBlob(foundKeys[i], foundValues[i]);
            }
        }

        public async Task ReadByPrefixAsync(string keyPrefix, Action<string, byte[]> onBlob)
        {
            List<string> keys = null;
            List<byte[]> values = null;

            await _gate.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    SQLiteConnection connection = GetConnection();
                    string rangeEnd = keyPrefix + RANGE_END_SUFFIX;

                    keys = connection.QueryScalars<string>("SELECT key FROM blobs WHERE key >= ? AND key < ? ORDER BY key", keyPrefix, rangeEnd);
                    values = connection.QueryScalars<byte[]>("SELECT data FROM blobs WHERE key >= ? AND key < ? ORDER BY key", keyPrefix, rangeEnd);
                });
            }
            finally
            {
                _gate.Release();
            }

            for (int i = 0; i < keys.Count; i++)
            {
                onBlob(keys[i], values[i]);
            }
        }

        public IBlobWriteBatch BeginWrite()
        {
            return new SqliteBlobWriteBatch(this);
        }

        public async Task DeleteByPrefixAsync(string keyPrefix)
        {
            await _gate.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    GetConnection().Execute("DELETE FROM blobs WHERE key >= ? AND key < ?", keyPrefix, keyPrefix + RANGE_END_SUFFIX);
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        internal async Task CommitBatchAsync(List<KeyValuePair<string, byte[]>> puts, List<string> deletes)
        {
            await _gate.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    SQLiteConnection connection = GetConnection();
                    connection.BeginTransaction();

                    try
                    {
                        if (puts.Count > 0)
                        {
                            using (SQLitePreparedStatement insert = new SQLitePreparedStatement(connection, "INSERT OR REPLACE INTO blobs (key, data) VALUES (?, ?)"))
                            {
                                foreach (KeyValuePair<string, byte[]> put in puts)
                                {
                                    insert.Bind(1, put.Key);
                                    insert.Bind(2, put.Value);
                                    insert.Step();
                                    insert.Reset();
                                }
                            }
                        }

                        if (deletes.Count > 0)
                        {
                            using (SQLitePreparedStatement delete = new SQLitePreparedStatement(connection, "DELETE FROM blobs WHERE key = ?"))
                            {
                                foreach (string key in deletes)
                                {
                                    delete.Bind(1, key);
                                    delete.Step();
                                    delete.Reset();
                                }
                            }
                        }

                        connection.Commit();
                    }
                    catch
                    {
                        connection.Rollback();
                        throw;
                    }

                    connection.ExecuteScalar<int>("PRAGMA wal_checkpoint(TRUNCATE)");
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        internal void CloseConnection()
        {
            _gate.Wait();

            try
            {
                if (_connection != null)
                {
                    _connection.Close();
                    _connection = null;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private SQLiteConnection GetConnection()
        {
            if (_connection == null)
            {
                _connection = new SQLiteConnection(_databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                _connection.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
                _connection.Execute("CREATE TABLE IF NOT EXISTS blobs (key TEXT PRIMARY KEY NOT NULL, data BLOB NOT NULL)");
            }

            return _connection;
        }
    }
}
