using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace MirraCloud.Core.Storage.Blob.Tests
{
    internal static class TaskCoroutine
    {
        public static IEnumerator Run(Func<Task> asyncAction)
        {
            Task task = Task.Run(asyncAction);

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception;

                if (task.Exception != null && task.Exception.InnerException != null)
                {
                    exception = task.Exception.InnerException;
                }

                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}
