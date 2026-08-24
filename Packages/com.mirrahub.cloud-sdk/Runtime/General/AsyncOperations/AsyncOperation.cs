using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Plugins.MirraCloud.Core.General.AsyncOperations
{
    public sealed class AsyncOperation : CustomYieldInstruction, IAsyncOperation
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public bool IsDone => _tcs.Task.IsCompleted;

        public override bool keepWaiting => IsDone == false;

        public event Action<IAsyncOperation> OnCompleted;
        private event Action<IAsyncOperation> _onCompletedCallback;
    
        public Task Task() => _tcs.Task;
        
        public void Complete()
        {
            _onCompletedCallback?.Invoke(this);

            if (_tcs.TrySetResult(true))
            {
                OnCompleted?.Invoke(this);
            }
        }

        public static AsyncOperation CreateCompleted()
        {
            var op = new AsyncOperation();
            op.Complete();
            return op;
        }
        
        public void UseCompleted(Action<IAsyncOperation> callback)
        {
            _onCompletedCallback = callback;
        }
    }

    public sealed class AsyncOperation<T> : CustomYieldInstruction, IAsyncOperation<T>
    {
        private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();
        private T _result;

        public bool IsDone => _tcs.Task.IsCompleted;
        
        public T Result => _result;
        public override bool keepWaiting => IsDone == false;

        public event Action<IAsyncOperation<T>> OnCompleted;
        
        private event Action<IAsyncOperation<T>> _onCompletedCallback;

        public Task Task() => _tcs.Task;
        
        public void Complete(T result)
        {
            _result = result;
            _onCompletedCallback?.Invoke(this);

            if (_tcs.TrySetResult(result))
            {
                OnCompleted?.Invoke(this);
            }
        }

        public static AsyncOperation<T> CreateCompleted(T result)
        {
            var op = new AsyncOperation<T>();
            op.Complete(result);
            return op;
        }

        public void UseCompleted(Action<IAsyncOperation<T>> callback)
        {
            _onCompletedCallback = callback;
        }
    }
}

