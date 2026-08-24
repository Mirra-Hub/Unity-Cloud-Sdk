using System;
using MirraCloud.Json;

namespace MirraCloud.Core
{
    public static class JsonValueMapper
    {
        public static T Map<T>(IJsonService jsonService, JsonValue value)
        {
            if (jsonService == null) throw new ArgumentNullException(nameof(jsonService));
            if (value == null) return default;

            var json = jsonService.ToJson(value);
            return jsonService.FromJson<T>(json);
        }

        public static bool TryMap<T>(IJsonService jsonService, JsonValue value, out T result)
        {
            try
            {
                result = Map<T>(jsonService, value);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}
