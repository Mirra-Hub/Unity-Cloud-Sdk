using System;

namespace Plugins.MirraCloud.Core.General.AsyncOperations
{
    public interface IAsyncOperation : IBaseAsyncOperation
    {
        event Action<IAsyncOperation> OnCompleted;
    }
    
    public interface IAsyncOperation<out T> : IBaseAsyncOperation
    {
        T Result { get; }
        event Action<IAsyncOperation<T>> OnCompleted;
    }
}