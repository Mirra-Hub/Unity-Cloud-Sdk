using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Json;

namespace MirraCloud.Core.Realtime.Connection
{
    internal sealed class JsonRealtimeSerializer : IRealtimeSerializer
    {
        private readonly IJsonService _jsonService;

        public JsonRealtimeSerializer(IJsonService jsonService)
        {
            _jsonService = jsonService;
        }

        public string Serialize<T>(T data)
        {
            return _jsonService.ToJson(data);
        }

        public T Deserialize<T>(string json)
        {
            return _jsonService.FromJson<T>(json);
        }
    }
}
