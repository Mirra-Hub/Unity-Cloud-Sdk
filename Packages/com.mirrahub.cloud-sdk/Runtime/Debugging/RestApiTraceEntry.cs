#if UNITY_EDITOR
using System;

namespace MirraCloud.Core.Debugging
{
    [Serializable]
    public sealed class RestApiTraceEntry
    {
        public int Id;
        public DateTime TimestampUtc;

        public string Method;
        public string Route;
        public string Url;

        public bool IsSuccess;
        public long? HttpStatusCode;
        public long DurationMs;
        public int RetryCount;

        public string RequestBody;
        public string ResponseBody;

        public RestApiError Error;
    }
}
#endif
