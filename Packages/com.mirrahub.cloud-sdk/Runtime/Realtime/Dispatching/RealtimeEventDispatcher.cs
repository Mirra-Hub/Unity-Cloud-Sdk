using System.Collections.Generic;
using MirraCloud.Core.Realtime.Protocol;

namespace MirraCloud.Core.Realtime.Dispatching
{
    public sealed class RealtimeEventDispatcher
    {
        private readonly Dictionary<string, IRealtimeEventHandler> _handlers = new Dictionary<string, IRealtimeEventHandler>();

        public void Register(string eventName, IRealtimeEventHandler handler)
        {
            _handlers[eventName] = handler;
        }

        public void Dispatch(RealtimeEvent realtimeEvent)
        {
            if (realtimeEvent == null || string.IsNullOrWhiteSpace(realtimeEvent.Name))
                return;

            if (_handlers.TryGetValue(realtimeEvent.Name, out var handler))
                handler.Handle(realtimeEvent);
        }
    }
}
