using System;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class MessageDeletedEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly Action<RealtimeDeletePayload> _onMessageDeleted;

        public MessageDeletedEventHandler(IJsonService jsonService, Action<RealtimeDeletePayload> onMessageDeleted)
        {
            _jsonService = jsonService;
            _onMessageDeleted = onMessageDeleted;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            var payload = Deserialize<RealtimeDeletePayload>(realtimeEvent.Payload);
            if (payload == null) return;

            _onMessageDeleted?.Invoke(payload);
        }

        private T Deserialize<T>(JsonValue value)
        {
            if (value == null) return default;
            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<T>(json);
        }
    }
}
