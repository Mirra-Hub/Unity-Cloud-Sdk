using System;
using MirraCloud.Core.Chats.Events;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class MessagePermissionsChangedEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly Action<ChatMessagePermissionsChangedEvent> _onChanged;

        public MessagePermissionsChangedEventHandler(IJsonService jsonService,
            Action<ChatMessagePermissionsChangedEvent> onChanged)
        {
            _jsonService = jsonService;
            _onChanged = onChanged;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            if (realtimeEvent.Payload == null) return;

            var payload = _jsonService.FromJson<RealtimeMessagePermissionsPayload>(
                _jsonService.ToJson(realtimeEvent.Payload));
            if (payload?.MessagePermissions == null) return;

            _onChanged?.Invoke(new ChatMessagePermissionsChangedEvent
            {
                ChannelId = string.IsNullOrWhiteSpace(payload.ChannelId) ? realtimeEvent.ChannelId : payload.ChannelId,
                MessagePermissions = payload.MessagePermissions
            });
        }
    }
}
