using System;
using MirraCloud.Core.Chats.Events;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class MemberEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly Action<ChatMemberEvent> _onMemberEvent;

        public MemberEventHandler(IJsonService jsonService, Action<ChatMemberEvent> onMemberEvent)
        {
            _jsonService = jsonService;
            _onMemberEvent = onMemberEvent;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            var payload = Deserialize<RealtimeMemberPayload>(realtimeEvent.Payload);
            if (payload == null) return;

            _onMemberEvent?.Invoke(new ChatMemberEvent
            {
                ChannelId = payload.ChannelId,
                ProfileId = payload.ProfileId
            });
        }

        private T Deserialize<T>(JsonValue value)
        {
            if (value == null) return default;
            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<T>(json);
        }
    }
}
