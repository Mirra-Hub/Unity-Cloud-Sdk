using System;
using System.Collections.Generic;
using MirraCloud.Json;

namespace MirraCloud.Core.Errors
{
    /// <summary>
    /// One typed error returned by the Cloud backend inside
    /// <see cref="ErrorResponseDto.Errors"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Code"/> is the authoritative dispatch key (see
    /// <see cref="CloudErrorCodes"/>). <see cref="Message"/> is a server-side
    /// fallback string suitable for logging; never show it to end-users
    /// directly — localise via your own UI layer based on <see cref="Code"/>.
    /// </para>
    /// <para>
    /// <see cref="Data"/> holds structured per-error context using the SDK's
    /// own JSON union type so it can carry any backend payload without
    /// reflection. Use <see cref="JsonValue"/> accessors (<c>(string)v</c>,
    /// <c>(int)v</c>, etc.) to read fields.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class CloudApiError
    {
        public string Code;
        public string Message;
        public Dictionary<string, JsonValue> Data;
    }
}
