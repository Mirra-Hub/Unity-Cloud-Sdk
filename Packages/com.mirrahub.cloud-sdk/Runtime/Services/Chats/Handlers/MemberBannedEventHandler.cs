using System;
using MirraCloud.Core.Chats.Events;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class MemberBannedEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly Action<ChatMemberBannedEvent> _onMemberBanned;

        public MemberBannedEventHandler(IJsonService jsonService, Action<ChatMemberBannedEvent> onMemberBanned)
        {
            _jsonService = jsonService;
            _onMemberBanned = onMemberBanned;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            var payload = Deserialize<RealtimeBanPayload>(realtimeEvent.Payload);
            if (payload == null) return;

            _onMemberBanned?.Invoke(new ChatMemberBannedEvent
            {
                ChannelId = payload.ChannelId,
                ProfileId = payload.ProfileId,
                BannedBy = payload.BannedBy
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
