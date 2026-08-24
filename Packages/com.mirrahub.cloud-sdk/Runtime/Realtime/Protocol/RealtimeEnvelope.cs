using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Protocol
{
    [Serializable]
    public sealed class RealtimeEnvelope
    {
        [JsonNameCamel] public string Kind;
        [JsonNameCamel] public string RequestId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public string ChannelId;
        [JsonNameCamel] public string Code;
        [JsonNameCamel] public string Message;
        [JsonNameCamel] public JsonValue Payload;
    }
}
