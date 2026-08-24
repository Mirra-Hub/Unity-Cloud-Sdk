using MirraCloud.Core.Realtime.Protocol;

namespace MirraCloud.Core.Realtime.Dispatching
{
    public interface IRealtimeEventHandler
    {
        void Handle(RealtimeEvent realtimeEvent);
    }
}
