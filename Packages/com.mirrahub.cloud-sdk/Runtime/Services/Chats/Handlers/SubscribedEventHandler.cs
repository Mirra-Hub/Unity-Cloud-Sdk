using System;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class SubscribedEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly IRealtimeSubscriptionStore _subscriptions;
        private readonly Action<string> _onSubscribed;

        public SubscribedEventHandler(
            IJsonService jsonService,
            IRealtimeSubscriptionStore subscriptions,
            Action<string> onSubscribed)
        {
            _jsonService = jsonService;
            _subscriptions = subscriptions;
            _onSubscribed = onSubscribed;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            var payload = Deserialize<RealtimeAckChannelPayload>(realtimeEvent.Payload);
            if (payload == null || string.IsNullOrWhiteSpace(payload.RoomId))
                return;

            _subscriptions.Add(payload.RoomId);
            _onSubscribed?.Invoke(payload.RoomId);
        }

        private T Deserialize<T>(JsonValue value)
        {
            if (value == null) return default;
            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<T>(json);
        }
    }
}
