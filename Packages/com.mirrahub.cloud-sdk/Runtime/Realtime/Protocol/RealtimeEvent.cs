using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Protocol
{
    public sealed class RealtimeEvent
    {
        public string Name;
        public string ChannelId;
        public JsonValue Payload;
    }
}
