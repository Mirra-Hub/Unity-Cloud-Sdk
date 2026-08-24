using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Realtime.Abstractions
{
    public interface IRealtimeRequestTracker
    {
        AsyncOperation<RealtimeCommandResult> Register(string requestId, int timeoutMs);
        void CompleteAck(string requestId, JsonValue payload);
        void CompleteError(string requestId, string code, string message);
        void FailAll(string code, string message);
    }
}
