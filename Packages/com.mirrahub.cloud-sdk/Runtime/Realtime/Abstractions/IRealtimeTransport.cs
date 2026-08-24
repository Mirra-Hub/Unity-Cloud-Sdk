using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MirraCloud.Core.Realtime.Abstractions
{
    public interface IRealtimeTransport
    {
        bool IsConnected { get; }

        event Action<string> OnMessage;
        event Action OnClosed;
        event Action<Exception> OnError;

        Task ConnectAsync(Uri uri, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);
        Task SendAsync(string message, CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}
