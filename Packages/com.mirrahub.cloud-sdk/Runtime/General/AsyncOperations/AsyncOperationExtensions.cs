using System;
using System.Threading.Tasks;

namespace Plugins.MirraCloud.Core.General.AsyncOperations
{
    public static class AsyncOperationExtensions
    {
        public static async Task<T> AsTask<T>(this AsyncOperation<T> operation)
        {
            await operation.Task();
            return operation.Result;
        }

        public static AsyncOperation<T> FromTask<T>(Func<Task<T>> work, Func<Exception, T> onError)
        {
            AsyncOperation<T> operation = new AsyncOperation<T>();
            RunToOperation(operation, work, onError);
            return operation;
        }

        private static async void RunToOperation<T>(AsyncOperation<T> operation, Func<Task<T>> work, Func<Exception, T> onError)
        {
            T result;

            try
            {
                result = await work();
            }
            catch (Exception exception)
            {
                result = onError(exception);
            }

            operation.Complete(result);
        }

        public static Task ToTask(this UnityEngine.AsyncOperation operation)
        {
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();

            if (operation.isDone)
            {
                completion.SetResult(true);
                return completion.Task;
            }

            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }
    }
}
