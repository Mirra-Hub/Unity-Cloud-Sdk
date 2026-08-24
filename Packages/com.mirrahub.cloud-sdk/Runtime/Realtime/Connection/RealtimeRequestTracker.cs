using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Realtime.Connection
{
    internal sealed class RealtimeRequestTracker : IRealtimeRequestTracker
    {
        private readonly Dictionary<string, AsyncOperation<RealtimeCommandResult>> _pending = new();

        public AsyncOperation<RealtimeCommandResult> Register(string requestId, int timeoutMs)
        {
            var op = new AsyncOperation<RealtimeCommandResult>();
            if (!_pending.TryAdd(requestId, op))
            {
                return AsyncOperation<RealtimeCommandResult>.CreateCompleted(
                    RealtimeCommandResult.Error(requestId, "duplicate_request", "Request already exists."));
            }

            var safeTimeout = Math.Max(1000, timeoutMs);
            var cts = new CancellationTokenSource(safeTimeout);
            cts.Token.Register(() =>
            {
                if (_pending.Remove(requestId, out var pending) && !pending.IsDone)
                {
                    pending.Complete(RealtimeCommandResult.Error(requestId, "timeout", "Realtime command timeout."));
                }

                cts.Dispose();
            });

            return op;
        }

        public void CompleteAck(string requestId, JsonValue payload)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return;

            if (_pending.Remove(requestId, out var op))
                op.Complete(RealtimeCommandResult.Success(requestId, payload));
        }

        public void CompleteError(string requestId, string code, string message)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return;

            if (_pending.Remove(requestId, out var op))
                op.Complete(RealtimeCommandResult.Error(requestId, code, message));
        }

        public void FailAll(string code, string message)
        {
            foreach (var pair in _pending)
            {
                if (!pair.Value.IsDone)
                    pair.Value.Complete(RealtimeCommandResult.Error(pair.Key, code, message));
            }

            _pending.Clear();
        }
    }
}
