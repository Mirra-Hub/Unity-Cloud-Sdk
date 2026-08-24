using System;
using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Protocol
{
    [Serializable]
    public sealed class RealtimeCommand
    {
        [JsonNameCamel] public string Kind = "command";
        [JsonNameCamel] public string RequestId;
        [JsonNameCamel] public string Name;
        [JsonNameCamel] public JsonValue Payload;
    }
}
