using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Protocol
{
    public sealed class RealtimeCommandResult
    {
        public bool IsSuccess;
        public string RequestId;
        public string Code;
        public string Message;
        public JsonValue Payload;

        public static RealtimeCommandResult Success(string requestId, JsonValue payload)
        {
            return new RealtimeCommandResult
            {
                IsSuccess = true,
                RequestId = requestId,
                Payload = payload
            };
        }

        public static RealtimeCommandResult Error(string requestId, string code, string message)
        {
            return new RealtimeCommandResult
            {
                IsSuccess = false,
                RequestId = requestId,
                Code = code,
                Message = message
            };
        }
    }
}
