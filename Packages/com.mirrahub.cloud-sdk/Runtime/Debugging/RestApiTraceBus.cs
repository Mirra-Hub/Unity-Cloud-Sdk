#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace MirraCloud.Core.Debugging
{
    public static class RestApiTraceBus
    {
        public const int Capacity = 300;
        public const int MaxBodyBytes = 128 * 1024;

        private static readonly List<RestApiTraceEntry> _entries = new List<RestApiTraceEntry>(Capacity);
        private static int _nextId = 1;

        public static bool IsEnabled { get; set; } = true;
        public static IReadOnlyList<RestApiTraceEntry> Entries => _entries;

        public static event Action OnChanged;

        public static void Clear()
        {
            _entries.Clear();
            OnChanged?.Invoke();
        }

        public static void Record(RestRequestConfig request, RestApiResult response, string requestBody)
        {
            if (IsEnabled == false)
            {
                return;
            }

            var entry = new RestApiTraceEntry
            {
                Id = _nextId++,
                TimestampUtc = DateTime.UtcNow,
                Method = request?.Method ?? response?.Method,
                Route = request?.TraceRoute ?? request?.Route ?? response?.Route,
                Url = request?.TraceUrl ?? request?.Url ?? response?.Url,
                IsSuccess = response?.IsSuccess ?? false,
                HttpStatusCode = response?.HttpStatusCode,
                DurationMs = response?.DurationMs ?? 0,
                RetryCount = response?.RetryCount ?? 0,
                Error = response?.Error,
                RequestBody = TruncateUtf8(requestBody, MaxBodyBytes),
                ResponseBody = TruncateUtf8(response?.ResponseBody, MaxBodyBytes)
            };

            if (_entries.Count >= Capacity)
            {
                _entries.RemoveAt(0);
            }
            _entries.Add(entry);
            OnChanged?.Invoke();
        }

        public static string TruncateUtf8(string value, int maxBytes)
        {
            if (string.IsNullOrEmpty(value) || maxBytes <= 0)
            {
                return value;
            }

            var utf8 = Encoding.UTF8;
            if (utf8.GetByteCount(value) <= maxBytes)
            {
                return value;
            }

            var chars = value.ToCharArray();
            var byteCount = 0;
            var length = 0;

            while (length < chars.Length)
            {
                var charBytes = utf8.GetByteCount(chars, length, 1);
                if (byteCount + charBytes > maxBytes)
                {
                    break;
                }
                byteCount += charBytes;
                length++;
            }

            return new string(chars, 0, length) + "\n[truncated]";
        }
    }
}
#endif
