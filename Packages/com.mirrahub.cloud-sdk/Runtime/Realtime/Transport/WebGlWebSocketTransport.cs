using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JamesFrowen.SimpleWeb;
using MirraCloud.Core.Logger;
using MirraCloud.Core.Realtime.Abstractions;

namespace MirraCloud.Core.Realtime.Transport
{
    /// <summary>
    /// <see cref="IRealtimeTransport"/> backed by the vendored James-Frowen SimpleWebClient.
    /// Selected by <see cref="RealtimeTransportFactory"/> for WebGL builds, where the default
    /// <see cref="ClientWebSocketTransport"/> (System.Net.WebSockets.ClientWebSocket) is not
    /// supported by the browser runtime.
    /// <para>
    /// The <c>headers</c> argument of <see cref="ConnectAsync"/> is ignored: the browser WebSocket
    /// API cannot set custom request headers. The realtime token is carried in the wsUrl query
    /// string, so no headers are required. Outgoing frames are binary; incoming TEXT frames are
    /// handled by a patch in SimpleWeb.jslib (see External/SimpleWebTransport/PATCHES.md).
    /// SimpleWebClient delivers events from a queue that must be pumped each frame via
    /// ProcessMessageQueue, driven here from a coroutine on the shared <see cref="ICoroutineRunner"/>.
    /// </para>
    /// </summary>
    internal sealed class WebGlWebSocketTransport : IRealtimeTransport
    {
        private const int MaxMessageSize = 64 * 1024;
        private const int MaxMessagesPerTick = 100;

        private readonly ILogger _logger;
        private readonly ICoroutineRunner _coroutineRunner;

        private SimpleWebClient _client;
        private TaskCompletionSource<bool> _connectTcs;
        private bool _pumping;
        private bool _closedNotified;

        public bool IsConnected => _client != null && _client.ConnectionState == ClientState.Connected;

        public event Action<string> OnMessage;
        public event Action OnClosed;
        public event Action<Exception> OnError;

        public WebGlWebSocketTransport(ILogger logger, ICoroutineRunner coroutineRunner)
        {
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public Task ConnectAsync(Uri uri, Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            if (_client != null)
                Cleanup();

            _closedNotified = false;
            _connectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // headers are intentionally ignored: the browser WebSocket API cannot set custom
            // request headers; the realtime token travels in the wsUrl query string.
            _client = SimpleWebClient.Create(MaxMessageSize, MaxMessagesPerTick, default);
            _client.onConnect += HandleConnect;
            _client.onData += HandleData;
            _client.onDisconnect += HandleDisconnect;
            _client.onError += HandleError;

            if (cancellationToken.CanBeCanceled)
                cancellationToken.Register(() => _connectTcs?.TrySetCanceled());

            _logger.Log($"[WS] (WebGL) Connecting to: {uri}");
            _client.Connect(uri);

            _pumping = true;
            _coroutineRunner.StartCoroutine(PumpLoop());

            return _connectTcs.Task;
        }

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket is not connected.");

            // SimpleWebClient.Send takes a ReadOnlySpan<byte>; byte[] converts implicitly.
            _client.Send(Encoding.UTF8.GetBytes(message));
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            var client = _client;
            Cleanup();

            try
            {
                client?.Disconnect();
            }
            catch
            {
            }

            NotifyClosedOnce();
            return Task.CompletedTask;
        }

        private void HandleConnect()
        {
            _connectTcs?.TrySetResult(true);
        }

        private void HandleData(ArraySegment<byte> data)
        {
            if (data.Array == null)
                return;

            string message;
            try
            {
                message = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                return;
            }

            OnMessage?.Invoke(message);
        }

        private void HandleDisconnect()
        {
            _pumping = false;

            if (_connectTcs != null && !_connectTcs.Task.IsCompleted)
                _connectTcs.TrySetException(
                    new Exception("Realtime connection closed before it was established."));

            NotifyClosedOnce();
        }

        private void HandleError(Exception exception)
        {
            var error = exception ?? new Exception("Realtime transport error.");

            if (_connectTcs != null && !_connectTcs.Task.IsCompleted)
                _connectTcs.TrySetException(error);

            OnError?.Invoke(error);
        }

        private IEnumerator PumpLoop()
        {
            while (_pumping)
            {
                _client?.ProcessMessageQueue();
                yield return null;
            }
        }

        private void Cleanup()
        {
            _pumping = false;

            var client = _client;
            _client = null;

            if (client == null)
                return;

            client.onConnect -= HandleConnect;
            client.onData -= HandleData;
            client.onDisconnect -= HandleDisconnect;
            client.onError -= HandleError;
        }

        private void NotifyClosedOnce()
        {
            if (_closedNotified)
                return;

            _closedNotified = true;
            OnClosed?.Invoke();
        }
    }
}
