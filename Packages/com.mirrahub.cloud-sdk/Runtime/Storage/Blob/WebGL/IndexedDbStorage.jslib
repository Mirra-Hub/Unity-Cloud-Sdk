var IndexedDbStorageLib = {
    $bwIdb: {
        db: null,
        opening: false,
        onReadyQueue: [],
        batches: {},
        locks: {},
        persistRequested: false,

        ensureDb: function (onReady) {
            if (bwIdb.db) {
                onReady(true);
                return;
            }

            bwIdb.onReadyQueue.push(onReady);

            if (bwIdb.opening) {
                return;
            }

            bwIdb.opening = true;

            var request = indexedDB.open('blockworld', 1);

            request.onupgradeneeded = function () {
                var db = request.result;
                if (db.objectStoreNames.contains('blobs') === false) {
                    db.createObjectStore('blobs');
                }
                if (db.objectStoreNames.contains('containers') === false) {
                    db.createObjectStore('containers');
                }
            };

            request.onsuccess = function () {
                bwIdb.db = request.result;
                bwIdb.flushReady(true);

                if (navigator.storage && navigator.storage.estimate) {
                    navigator.storage.estimate().then(function (estimate) {
                        if (estimate.quota > 0 && estimate.usage / estimate.quota > 0.9) {
                            console.warn('[BwIdb] storage quota almost full: ' + estimate.usage + ' / ' + estimate.quota);
                        }
                    });
                }
            };

            request.onerror = function () {
                console.error('[BwIdb] failed to open database:', request.error);
                bwIdb.db = null;
                bwIdb.flushReady(false);
            };
        },

        flushReady: function (ok) {
            bwIdb.opening = false;
            var queue = bwIdb.onReadyQueue;
            bwIdb.onReadyQueue = [];
            for (var i = 0; i < queue.length; i++) {
                queue[i](ok);
            }
        },

        allocBytes: function (bytes) {
            var ptr = _malloc(bytes.length);
            HEAPU8.set(bytes, ptr);
            return ptr;
        },

        allocString: function (str) {
            var size = lengthBytesUTF8(str) + 1;
            var ptr = _malloc(size);
            stringToUTF8(str, ptr, size);
            return ptr;
        },

        requestPersist: function () {
            if (bwIdb.persistRequested) {
                return;
            }
            bwIdb.persistRequested = true;

            if (navigator.storage && navigator.storage.persist) {
                navigator.storage.persist().then(function (granted) {
                    console.log('[BwIdb] persistent storage granted: ' + granted);
                });
            }
        }
    },

    BwIdbGet: function (requestId, keyPtr, callback) {
        var key = UTF8ToString(keyPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('viiii', callback, [requestId, 2, 0, 0]);
                return;
            }

            var request = bwIdb.db.transaction('blobs', 'readonly').objectStore('blobs').get(key);

            request.onsuccess = function () {
                if (request.result === undefined) {
                    dynCall('viiii', callback, [requestId, 1, 0, 0]);
                    return;
                }

                var bytes = new Uint8Array(request.result);
                var ptr = bwIdb.allocBytes(bytes);
                dynCall('viiii', callback, [requestId, 0, ptr, bytes.length]);
                _free(ptr);
            };

            request.onerror = function () {
                console.error('[BwIdb] get failed:', request.error);
                dynCall('viiii', callback, [requestId, 2, 0, 0]);
            };
        });
    },

    BwIdbExists: function (requestId, keyPtr, callback) {
        var key = UTF8ToString(keyPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('viii', callback, [requestId, 2, 0]);
                return;
            }

            var request = bwIdb.db.transaction('blobs', 'readonly').objectStore('blobs').count(IDBKeyRange.only(key));

            request.onsuccess = function () {
                dynCall('viii', callback, [requestId, 0, request.result > 0 ? 1 : 0]);
            };

            request.onerror = function () {
                dynCall('viii', callback, [requestId, 2, 0]);
            };
        });
    },

    BwIdbReadPrefix: function (requestId, prefixPtr, itemCallback, doneCallback) {
        var prefix = UTF8ToString(prefixPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('vii', doneCallback, [requestId, 2]);
                return;
            }

            var range = IDBKeyRange.bound(prefix, prefix + '\uffff');
            var store = bwIdb.db.transaction('blobs', 'readonly').objectStore('blobs');
            var keysRequest = store.getAllKeys(range);
            var valuesRequest = store.getAll(range);
            var keys = null;
            var values = null;
            var failed = false;

            var finish = function () {
                if (keys === null || values === null) {
                    return;
                }

                for (var i = 0; i < keys.length; i++) {
                    var bytes = new Uint8Array(values[i]);
                    var keyPtr = bwIdb.allocString(keys[i]);
                    var dataPtr = bwIdb.allocBytes(bytes);
                    dynCall('viiii', itemCallback, [requestId, keyPtr, dataPtr, bytes.length]);
                    _free(keyPtr);
                    _free(dataPtr);
                }

                dynCall('vii', doneCallback, [requestId, 0]);
            };

            var fail = function (error) {
                if (failed) {
                    return;
                }
                failed = true;
                console.error('[BwIdb] read prefix failed:', error);
                dynCall('vii', doneCallback, [requestId, 2]);
            };

            keysRequest.onsuccess = function () { keys = keysRequest.result; finish(); };
            valuesRequest.onsuccess = function () { values = valuesRequest.result; finish(); };
            keysRequest.onerror = function () { fail(keysRequest.error); };
            valuesRequest.onerror = function () { fail(valuesRequest.error); };
        });
    },

    BwIdbGetMany: function (requestId, joinedKeysPtr, itemCallback, doneCallback) {
        var keys = UTF8ToString(joinedKeysPtr).split('\n');
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('vii', doneCallback, [requestId, 2]);
                return;
            }

            var store = bwIdb.db.transaction('blobs', 'readonly').objectStore('blobs');
            var foundKeys = [];
            var foundValues = [];
            var remaining = keys.length;

            var finishOne = function () {
                remaining--;
                if (remaining > 0) {
                    return;
                }

                for (var i = 0; i < foundKeys.length; i++) {
                    var bytes = new Uint8Array(foundValues[i]);
                    var keyPtr = bwIdb.allocString(foundKeys[i]);
                    var dataPtr = bwIdb.allocBytes(bytes);
                    dynCall('viiii', itemCallback, [requestId, keyPtr, dataPtr, bytes.length]);
                    _free(keyPtr);
                    _free(dataPtr);
                }

                dynCall('vii', doneCallback, [requestId, 0]);
            };

            keys.forEach(function (key) {
                var request = store.get(key);

                request.onsuccess = function () {
                    if (request.result !== undefined) {
                        foundKeys.push(key);
                        foundValues.push(request.result);
                    }
                    finishOne();
                };

                request.onerror = function () {
                    console.error('[BwIdb] getMany item failed:', request.error);
                    finishOne();
                };
            });
        });
    },

    BwIdbBatchBegin: function (batchId) {
        bwIdb.batches[batchId] = { puts: [], deletes: [] };
    },

    BwIdbBatchPut: function (batchId, keyPtr, dataPtr, dataLength) {
        bwIdb.batches[batchId].puts.push({
            key: UTF8ToString(keyPtr),
            data: HEAPU8.slice(dataPtr, dataPtr + dataLength)
        });
    },

    BwIdbBatchDelete: function (batchId, keyPtr) {
        bwIdb.batches[batchId].deletes.push(UTF8ToString(keyPtr));
    },

    BwIdbBatchCommit: function (batchId, requestId, containerIdPtr, callback) {
        var containerId = UTF8ToString(containerIdPtr);
        var batch = bwIdb.batches[batchId];
        delete bwIdb.batches[batchId];

        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('vii', callback, [requestId, 2]);
                return;
            }

            var tx = bwIdb.db.transaction(['blobs', 'containers'], 'readwrite');
            var blobs = tx.objectStore('blobs');

            for (var i = 0; i < batch.puts.length; i++) {
                blobs.put(batch.puts[i].data, batch.puts[i].key);
            }

            for (var j = 0; j < batch.deletes.length; j++) {
                blobs.delete(batch.deletes[j]);
            }

            tx.objectStore('containers').put(1, containerId);

            tx.oncomplete = function () {
                bwIdb.requestPersist();
                dynCall('vii', callback, [requestId, 0]);
            };

            tx.onerror = tx.onabort = function () {
                console.error('[BwIdb] batch commit failed:', tx.error);
                dynCall('vii', callback, [requestId, 2]);
            };
        });
    },

    BwIdbDeletePrefix: function (requestId, prefixPtr, callback) {
        var prefix = UTF8ToString(prefixPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('vii', callback, [requestId, 2]);
                return;
            }

            var tx = bwIdb.db.transaction('blobs', 'readwrite');
            tx.objectStore('blobs').delete(IDBKeyRange.bound(prefix, prefix + '\uffff'));

            tx.oncomplete = function () {
                dynCall('vii', callback, [requestId, 0]);
            };

            tx.onerror = tx.onabort = function () {
                console.error('[BwIdb] delete prefix failed:', tx.error);
                dynCall('vii', callback, [requestId, 2]);
            };
        });
    },

    BwIdbDeleteContainer: function (requestId, containerIdPtr, callback) {
        var containerId = UTF8ToString(containerIdPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('vii', callback, [requestId, 2]);
                return;
            }

            var prefix = containerId + '/';
            var tx = bwIdb.db.transaction(['blobs', 'containers'], 'readwrite');
            tx.objectStore('blobs').delete(IDBKeyRange.bound(prefix, prefix + '\uffff'));
            tx.objectStore('containers').delete(containerId);

            tx.oncomplete = function () {
                dynCall('vii', callback, [requestId, 0]);
            };

            tx.onerror = tx.onabort = function () {
                console.error('[BwIdb] delete container failed:', tx.error);
                dynCall('vii', callback, [requestId, 2]);
            };
        });
    },

    BwIdbListContainers: function (requestId, prefixPtr, callback) {
        var prefix = UTF8ToString(prefixPtr);
        bwIdb.ensureDb(function (ok) {
            if (ok === false) {
                dynCall('viii', callback, [requestId, 2, 0]);
                return;
            }

            var store = bwIdb.db.transaction('containers', 'readonly').objectStore('containers');
            var request = prefix.length > 0
                ? store.getAllKeys(IDBKeyRange.bound(prefix, prefix + '\uffff'))
                : store.getAllKeys();

            request.onsuccess = function () {
                var joined = request.result.join('\n');
                var ptr = bwIdb.allocString(joined);
                dynCall('viii', callback, [requestId, 0, ptr]);
                _free(ptr);
            };

            request.onerror = function () {
                console.error('[BwIdb] list containers failed:', request.error);
                dynCall('viii', callback, [requestId, 2, 0]);
            };
        });
    },

    BwIdbAcquireContainerLock: function (requestId, namePtr, callback) {
        var name = 'bw_' + UTF8ToString(namePtr);

        if (typeof navigator === 'undefined' || !navigator.locks) {
            dynCall('vii', callback, [requestId, 1]);
            return;
        }

        if (bwIdb.locks[name]) {
            dynCall('vii', callback, [requestId, 1]);
            return;
        }

        navigator.locks.request(name, { ifAvailable: true }, function (lock) {
            if (lock === null) {
                dynCall('vii', callback, [requestId, 0]);
                return null;
            }

            bwIdb.locks[name] = true;
            dynCall('vii', callback, [requestId, 1]);
            return new Promise(function () {});
        }).catch(function (error) {
            console.error('[BwIdb] lock request failed:', error);
            dynCall('vii', callback, [requestId, 1]);
        });
    }
};

autoAddDeps(IndexedDbStorageLib, '$bwIdb');
mergeInto(LibraryManager.library, IndexedDbStorageLib);
