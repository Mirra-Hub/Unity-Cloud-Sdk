#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob
{
    internal static class IndexedDbNative
    {
        private sealed class PrefixRequest
        {
            public Action<string, byte[]> OnBlob;
            public TaskCompletionSource<bool> Completion;
        }

        private static int _nextRequestId;
        private static int _nextBatchId;

        private static readonly Dictionary<int, TaskCompletionSource<BlobResult>> _getRequests = new Dictionary<int, TaskCompletionSource<BlobResult>>();
        private static readonly Dictionary<int, TaskCompletionSource<bool>> _existsRequests = new Dictionary<int, TaskCompletionSource<bool>>();
        private static readonly Dictionary<int, TaskCompletionSource<bool>> _statusRequests = new Dictionary<int, TaskCompletionSource<bool>>();
        private static readonly Dictionary<int, TaskCompletionSource<string[]>> _listRequests = new Dictionary<int, TaskCompletionSource<string[]>>();
        private static readonly Dictionary<int, TaskCompletionSource<bool>> _lockRequests = new Dictionary<int, TaskCompletionSource<bool>>();
        private static readonly Dictionary<int, PrefixRequest> _prefixRequests = new Dictionary<int, PrefixRequest>();

        public static Task<BlobResult> GetAsync(string key)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<BlobResult> source = new TaskCompletionSource<BlobResult>();
            _getRequests.Add(requestId, source);
            BwIdbGet(requestId, key, OnGetCompleted);
            return source.Task;
        }

        public static Task<bool> ExistsAsync(string key)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
            _existsRequests.Add(requestId, source);
            BwIdbExists(requestId, key, OnExistsCompleted);
            return source.Task;
        }

        public static Task<bool> ReadPrefixAsync(string prefix, Action<string, byte[]> onBlob)
        {
            int requestId = _nextRequestId++;
            PrefixRequest request = new PrefixRequest
            {
                OnBlob = onBlob,
                Completion = new TaskCompletionSource<bool>(),
            };
            _prefixRequests.Add(requestId, request);
            BwIdbReadPrefix(requestId, prefix, OnPrefixItem, OnPrefixCompleted);
            return request.Completion.Task;
        }

        public static Task<bool> GetManyAsync(string[] keys, Action<string, byte[]> onBlob)
        {
            int requestId = _nextRequestId++;
            PrefixRequest request = new PrefixRequest
            {
                OnBlob = onBlob,
                Completion = new TaskCompletionSource<bool>(),
            };
            _prefixRequests.Add(requestId, request);
            BwIdbGetMany(requestId, string.Join("\n", keys), OnPrefixItem, OnPrefixCompleted);
            return request.Completion.Task;
        }

        public static int BeginBatch()
        {
            int batchId = _nextBatchId++;
            BwIdbBatchBegin(batchId);
            return batchId;
        }

        public static void BatchPut(int batchId, string key, byte[] data)
        {
            BwIdbBatchPut(batchId, key, data, data.Length);
        }

        public static void BatchDelete(int batchId, string key)
        {
            BwIdbBatchDelete(batchId, key);
        }

        public static Task<bool> CommitBatchAsync(int batchId, string containerId)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
            _statusRequests.Add(requestId, source);
            BwIdbBatchCommit(batchId, requestId, containerId, OnStatusCompleted);
            return source.Task;
        }

        public static Task<bool> DeletePrefixAsync(string prefix)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
            _statusRequests.Add(requestId, source);
            BwIdbDeletePrefix(requestId, prefix, OnStatusCompleted);
            return source.Task;
        }

        public static Task<bool> DeleteContainerAsync(string containerId)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
            _statusRequests.Add(requestId, source);
            BwIdbDeleteContainer(requestId, containerId, OnStatusCompleted);
            return source.Task;
        }

        public static Task<string[]> ListContainersAsync(string prefix)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<string[]> source = new TaskCompletionSource<string[]>();
            _listRequests.Add(requestId, source);
            BwIdbListContainers(requestId, prefix, OnListCompleted);
            return source.Task;
        }

        public static Task<bool> AcquireContainerLockAsync(string containerId)
        {
            int requestId = _nextRequestId++;
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
            _lockRequests.Add(requestId, source);
            BwIdbAcquireContainerLock(requestId, containerId, OnLockCompleted);
            return source.Task;
        }

        [MonoPInvokeCallback(typeof(Action<int, int, IntPtr, int>))]
        private static void OnGetCompleted(int requestId, int status, IntPtr dataPtr, int dataLength)
        {
            if (_getRequests.TryGetValue(requestId, out TaskCompletionSource<BlobResult> source) == false)
            {
                return;
            }

            _getRequests.Remove(requestId);

            if (status == 0)
            {
                byte[] data = new byte[dataLength];
                Marshal.Copy(dataPtr, data, 0, dataLength);
                source.TrySetResult(BlobResult.Ok(data));
            }
            else if (status == 1)
            {
                source.TrySetResult(BlobResult.NotFound());
            }
            else
            {
                source.TrySetResult(BlobResult.Error());
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, int, int>))]
        private static void OnExistsCompleted(int requestId, int status, int exists)
        {
            if (_existsRequests.TryGetValue(requestId, out TaskCompletionSource<bool> source) == false)
            {
                return;
            }

            _existsRequests.Remove(requestId);
            source.TrySetResult(status == 0 && exists == 1);
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, IntPtr, int>))]
        private static void OnPrefixItem(int requestId, IntPtr keyPtr, IntPtr dataPtr, int dataLength)
        {
            if (_prefixRequests.TryGetValue(requestId, out PrefixRequest request) == false)
            {
                return;
            }

            string key = Marshal.PtrToStringUTF8(keyPtr);
            byte[] data = new byte[dataLength];
            Marshal.Copy(dataPtr, data, 0, dataLength);
            request.OnBlob(key, data);
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnPrefixCompleted(int requestId, int status)
        {
            if (_prefixRequests.TryGetValue(requestId, out PrefixRequest request) == false)
            {
                return;
            }

            _prefixRequests.Remove(requestId);
            request.Completion.TrySetResult(status == 0);
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnStatusCompleted(int requestId, int status)
        {
            if (_statusRequests.TryGetValue(requestId, out TaskCompletionSource<bool> source) == false)
            {
                return;
            }

            _statusRequests.Remove(requestId);
            source.TrySetResult(status == 0);
        }

        [MonoPInvokeCallback(typeof(Action<int, int, IntPtr>))]
        private static void OnListCompleted(int requestId, int status, IntPtr joinedPtr)
        {
            if (_listRequests.TryGetValue(requestId, out TaskCompletionSource<string[]> source) == false)
            {
                return;
            }

            _listRequests.Remove(requestId);

            if (status != 0)
            {
                source.TrySetResult(Array.Empty<string>());
                return;
            }

            string joined = Marshal.PtrToStringUTF8(joinedPtr);

            if (string.IsNullOrEmpty(joined))
            {
                source.TrySetResult(Array.Empty<string>());
                return;
            }

            source.TrySetResult(joined.Split('\n'));
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnLockCompleted(int requestId, int held)
        {
            if (_lockRequests.TryGetValue(requestId, out TaskCompletionSource<bool> source) == false)
            {
                return;
            }

            _lockRequests.Remove(requestId);
            source.TrySetResult(held == 1);
        }

        [DllImport("__Internal")]
        private static extern void BwIdbGet(int requestId, string key, Action<int, int, IntPtr, int> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbExists(int requestId, string key, Action<int, int, int> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbReadPrefix(int requestId, string prefix, Action<int, IntPtr, IntPtr, int> itemCallback, Action<int, int> doneCallback);

        [DllImport("__Internal")]
        private static extern void BwIdbGetMany(int requestId, string joinedKeys, Action<int, IntPtr, IntPtr, int> itemCallback, Action<int, int> doneCallback);

        [DllImport("__Internal")]
        private static extern void BwIdbBatchBegin(int batchId);

        [DllImport("__Internal")]
        private static extern void BwIdbBatchPut(int batchId, string key, byte[] data, int dataLength);

        [DllImport("__Internal")]
        private static extern void BwIdbBatchDelete(int batchId, string key);

        [DllImport("__Internal")]
        private static extern void BwIdbBatchCommit(int batchId, int requestId, string containerId, Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbDeletePrefix(int requestId, string prefix, Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbDeleteContainer(int requestId, string containerId, Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbListContainers(int requestId, string prefix, Action<int, int, IntPtr> callback);

        [DllImport("__Internal")]
        private static extern void BwIdbAcquireContainerLock(int requestId, string containerId, Action<int, int> callback);
    }
}
#endif
