using System;
using MirraCloud.Core.Chats.Dto;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Json;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class MessageCreatedEventHandler : IRealtimeEventHandler
    {
        private readonly IJsonService _jsonService;
        private readonly Action<ChatMessageDto> _onMessageCreated;

        public MessageCreatedEventHandler(IJsonService jsonService, Action<ChatMessageDto> onMessageCreated)
        {
            _jsonService = jsonService;
            _onMessageCreated = onMessageCreated;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            var dto = Deserialize<ChatMessageDto>(realtimeEvent.Payload);
            if (dto == null) return;

            _onMessageCreated?.Invoke(dto);
        }

        private T Deserialize<T>(JsonValue value)
        {
            if (value == null) return default;
            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<T>(json);
        }
    }
}
