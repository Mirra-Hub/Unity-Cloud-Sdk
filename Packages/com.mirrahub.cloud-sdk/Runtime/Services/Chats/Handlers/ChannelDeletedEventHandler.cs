using System;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;

namespace MirraCloud.Core.Chats.Handlers
{
    internal sealed class ChannelDeletedEventHandler : IRealtimeEventHandler
    {
        private readonly Action<string> _onChannelDeleted;

        public ChannelDeletedEventHandler(Action<string> onChannelDeleted)
        {
            _onChannelDeleted = onChannelDeleted;
        }

        public void Handle(RealtimeEvent realtimeEvent)
        {
            if (string.IsNullOrWhiteSpace(realtimeEvent.ChannelId))
                return;

            _onChannelDeleted?.Invoke(realtimeEvent.ChannelId);
        }
    }
}
