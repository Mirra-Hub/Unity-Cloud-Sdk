using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.Logger;
using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Realtime.Connection
{
    internal sealed class RealtimeConnection : IRealtimeConnection
    {
        private readonly IRealtimeTransport _transport;
        private readonly IRealtimeSerializer _serializer;
        private readonly IRealtimeRequestTracker _requestTracker;
        private readonly ILogger _logger;

        public bool IsConnected => _transport.IsConnected;
        public RealtimeConnectionState State { get; private set; } = RealtimeConnectionState.Disconnected;

        public event Action<RealtimeConnectionState> OnStateChanged;
        public event Action<RealtimeEvent> OnEvent;
        public event Action<RealtimeError> OnError;

        public RealtimeConnection(
            IRealtimeTransport transport,
            IRealtimeSerializer serializer,
            IRealtimeRequestTracker requestTracker,
            ILogger logger)
        {
            _transport = transport;
            _serializer = serializer;
            _requestTracker = requestTracker;
            _logger = logger;
        }

        public void Initialize()
        {
            _transport.OnMessage += HandleTransportMessage;
            _transport.OnClosed += HandleTransportClosed;
            _transport.OnError += HandleTransportError;
        }

        public void Dispose()
        {
            _transport.OnMessage -= HandleTransportMessage;
            _transport.OnClosed -= HandleTransportClosed;
            _transport.OnError -= HandleTransportError;
        }

        public AsyncOperation<RealtimeCommandResult> Connect(string wsUrl, Dictionary<string, string> headers = null)
        {
            if (State is RealtimeConnectionState.Connected or RealtimeConnectionState.Connecting)
                return AsyncOperation<RealtimeCommandResult>.CreateCompleted(
                    RealtimeCommandResult.Success(string.Empty, default));

            SetState(RealtimeConnectionState.Connecting);
            var op = new AsyncOperation<RealtimeCommandResult>();
            ConnectTransportAsync(wsUrl, headers, op);
            return op;
        }

        private async void ConnectTransportAsync(string wsUrl, Dictionary<string, string> headers, AsyncOperation<RealtimeCommandResult> op)
        {
            try
            {
                await _transport.ConnectAsync(new Uri(wsUrl), headers);
                SetState(RealtimeConnectionState.Connected);
                op.Complete(RealtimeCommandResult.Success(string.Empty, default));
            }
            catch (Exception ex)
            {
                SetState(RealtimeConnectionState.Disconnected);
                op.Complete(RealtimeCommandResult.Error(string.Empty, "connect_failed", ex.Message));
            }
        }

        public AsyncOperation<RealtimeCommandResult> Disconnect()
        {
            if (State == RealtimeConnectionState.Disconnected)
                return AsyncOperation<RealtimeCommandResult>.CreateCompleted(
                    RealtimeCommandResult.Success(string.Empty, default));

            var op = new AsyncOperation<RealtimeCommandResult>();
            DisconnectTransportAsync(op);
            return op;
        }

        private async void DisconnectTransportAsync(AsyncOperation<RealtimeCommandResult> op)
        {
            try
            {
                await _transport.CloseAsync();
                _requestTracker.FailAll("connection_closed", "Realtime connection closed.");
                SetState(RealtimeConnectionState.Disconnected);
                op.Complete(RealtimeCommandResult.Success(string.Empty, default));
            }
            catch (Exception ex)
            {
                op.Complete(RealtimeCommandResult.Error(string.Empty, "disconnect_failed", ex.Message));
            }
        }

        public AsyncOperation<RealtimeCommandResult> SendCommand(string name, object payload, int timeoutMs = 10000)
        {
            if (!IsConnected)
                return AsyncOperation<RealtimeCommandResult>.CreateCompleted(
                    RealtimeCommandResult.Error(string.Empty, "not_connected",
                        "Realtime connection is not established."));

            var requestId = Guid.NewGuid().ToString("N");
            var op = _requestTracker.Register(requestId, timeoutMs);

            var command = new RealtimeCommand
            {
                RequestId = requestId,
                Name = name,
                Payload = payload is JsonValue jsonValue
                    ? jsonValue
                    : _serializer.Deserialize<JsonValue>(_serializer.Serialize(payload))
            };

            var json = _serializer.Serialize(command);
            SendTransportAsync(json, requestId);

            return op;
        }

        private async void SendTransportAsync(string json, string requestId)
        {
            try
            {
                await _transport.SendAsync(json);
            }
            catch (Exception ex)
            {
                _requestTracker.CompleteError(requestId, "send_failed", ex.Message);
            }
        }

        private void HandleTransportMessage(string json)
        {
            RealtimeEnvelope envelope;

            try
            {
                envelope = _serializer.Deserialize<RealtimeEnvelope>(json);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to parse realtime envelope: {ex.Message}");
                return;
            }

            var kind = envelope?.Kind ?? string.Empty;

            if (string.Equals(kind, "ack", StringComparison.OrdinalIgnoreCase))
            {
                _requestTracker.CompleteAck(envelope.RequestId, envelope.Payload);
                return;
            }

            if (string.Equals(kind, "error", StringComparison.OrdinalIgnoreCase))
            {
                _requestTracker.CompleteError(envelope.RequestId, envelope.Code, envelope.Message);
                OnError?.Invoke(new RealtimeError
                {
                    RequestId = envelope.RequestId,
                    Code = envelope.Code,
                    Message = envelope.Message
                });
                return;
            }

            if (string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase))
            {
                OnEvent?.Invoke(new RealtimeEvent
                {
                    Name = envelope.Name,
                    ChannelId = envelope.ChannelId,
                    Payload = envelope.Payload
                });
            }
        }

        private void HandleTransportClosed()
        {
            _requestTracker.FailAll("connection_closed", "Realtime connection closed.");
            SetState(RealtimeConnectionState.Disconnected);
        }

        private void HandleTransportError(Exception ex)
        {
            _logger.Error($"Realtime transport error: {ex.Message}");
            OnError?.Invoke(new RealtimeError
            {
                Code = "transport_error",
                Message = ex.Message
            });
        }

        private void SetState(RealtimeConnectionState state)
        {
            if (State == state)
                return;

            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
